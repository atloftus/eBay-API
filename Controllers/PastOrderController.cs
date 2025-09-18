using eBay_API.Models.Config;
using eBay_API.Models.Controller.Order;
using eBay_API.Models.eBay.Response;
using eBay_API.Models.GoogleDrive;
using eBay_API.Services;
using eBay_API.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;



namespace eBay_API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public sealed class PastOrderController : ControllerBase
    {
        #region PROPERTIES
        private readonly ILogger<PastOrderController> _logger;
        private readonly Config _config;
        private readonly EbayService _ebayService;
        private readonly GoogleDriveService _sheetService;
        #endregion



        #region CONSTRUCTORS
        /// <summary>
        /// Initializes a new instance of the <see cref="PastOrderController"/> class.
        /// </summary>
        /// <param name="logger">Logger for diagnostic output.</param>
        /// <param name="options">Configuration options injected from app settings.</param>
        /// <param name="ebayService">Service for eBay API operations.</param>
        /// <param name="sheetService">Service for Google Sheets operations.</param>
        /// <exception cref="ArgumentNullException">Thrown if options is null.</exception>
        public PastOrderController(ILogger<PastOrderController> logger, IOptions<Config> options, EbayService ebayService, GoogleDriveService sheetService)
        {
            _logger = logger;
            _config = options.Value ?? throw new ArgumentNullException(nameof(options));
            _ebayService = ebayService;
            _sheetService = sheetService;
        }
        #endregion



        #region METHODS
        /// <summary>
        /// Fetches buyer line items from eBay for the past 30 days and returns the count.
        /// </summary>
        /// <returns>
        /// 200 OK with the number of items fetched.
        /// </returns>
        [HttpPost("WriteOrderItemsToTable")]
        public async Task<IActionResult> WriteOrderItemsToTable()
        {
            string tabName = "ORDERS";
            var centralZone = DateTimeUtil.FindCentralTimeZone();
            var purchases = await _ebayService.GetBuyerLineItemsAsync(30);
            var orderItems = purchases.Select(OrderItem.FromLineItem).ToList();

            //Make sure that the tab exists
            await _sheetService.CreateTabAsync(tabName, orderItems.First().GetHeaderRow());

            // Get all current rows in the ORDERS TAB
            var existingOrderItems = await _sheetService.GetAllRowsAsync<OrderItem>(
                tabName,
                row => OrderItemUtil.ToOrderItem(row)
                );

            var combinedOrderItems = existingOrderItems.Concat(orderItems).ToList();

            //Remove duplicates based on ItemId, keeping the one with a shipping cost value
            //If both have or both don't have, keep the most recent one
            var allOrderItems = combinedOrderItems
                .GroupBy(oi => oi.ItemId)
                .Select(g =>
                {
                    // Prefer items with a shipping cost value
                    var withShipping = g.Where(x => !string.IsNullOrWhiteSpace(x.ShippingAmount) && x.ShippingAmount != "0" && x.ShippingAmount != "0.00");
                    IEnumerable<OrderItem> candidates = withShipping.Any() ? withShipping : g;

                    // Parse Created as DateTime, fallback to DateTime.MinValue if invalid
                    return candidates
                        .OrderByDescending(x =>
                        {
                            DateTime dt;
                            return DateTime.TryParse(x.Created, out dt) ? dt : DateTime.MinValue;
                        })
                        .First();
                })
                .ToList();

            await _sheetService.CreateTabAsync(tabName, allOrderItems.First().GetHeaderRow());
            await _sheetService.ClearFiltersAsync(tabName);
            await _sheetService.DeleteAllRowsExceptHeaderAsync(tabName);
            await _sheetService.WriteItemsAsync(
                allOrderItems,
                tabName,
                ai => ai.ToRow(),
                allOrderItems.First().GetHeaderRow()
            );

            await _sheetService.SetBasicOrdersFilterAsync(tabName);

            return Ok(new { items = orderItems.Count });
        }


        /// <summary>
        /// Synchronizes eBay orders, upserts inventory, creates and publishes offers, and optionally returns CSV data.
        /// </summary>
        /// <param name="req">SyncOrdersRequest containing sync options.</param>
        /// <returns>
        /// 200 OK with <see cref="SyncOrdersResponse"/> containing sync results.
        /// </returns>
        [HttpPost("sync")]
        public async Task<ActionResult<SyncOrdersResponse>> Sync([FromBody] SyncOrdersRequest req)
        {
            var days = req.LookbackDays ?? 30;
            var condition = _config.ebay.OmitCondition ? null : _ebayService.NormalizeCondition(req.ConditionInput ?? _config.ebay.DefaultCondition);
            var createOffers = req.CreateOffers ?? true;
            var publishFirst = req.PublishFirst ?? true;
            var fallbackPrice = req.FallbackPrice ?? _config.ebay.FallbackPrice;
            var categoryFallback = !string.IsNullOrWhiteSpace(req.CategoryFallback) ? req.CategoryFallback! : _config.ebay.CategoryFallback;

            var purchases = await _ebayService.GetBuyerLineItemsAsync(days, req.LimitForTesting);
            var createdSkus = new List<string>();

            foreach (var li in purchases)
            {
                var sku = _ebayService.MakeSku(li);
                var ok = await _ebayService.PutInventoryItemAsync(sku, li.Title, condition);
                if (ok) createdSkus.Add(sku);
            }

            var offers = new List<(string sku, string offerId)>();
            if (createOffers)
            {
                foreach (var li in purchases)
                {
                    var sku = _ebayService.MakeSku(li);
                    var price = li.Price ?? fallbackPrice;
                    if (price is null) continue;

                    // CategoryId and Currency removed from LineItem, use fallback and "USD"
                    var offerId = await _ebayService.CreateOfferDraftAsync(sku, categoryFallback, price.Value, "USD");
                    if (!string.IsNullOrEmpty(offerId)) offers.Add((sku, offerId));
                }

                if (publishFirst && offers.Count > 0)
                {
                    var first = offers[0];
                    await _ebayService.PublishOfferAsync(first.offerId);
                }
            }

            var csvBytes = req.IncludeCsv == true
                ? _ebayService.ToCsvBytes(purchases, createdSkus, offers)
                : null;

            var response = new SyncOrdersResponse
            {
                LookbackDays = days,
                Purchases = purchases.Count,
                InventoryUpserted = createdSkus.Count,
                DraftOffers = offers.Count,
                Published = (publishFirst && offers.Count > 0) ? 1 : 0,
                CreatedSkus = createdSkus,
                Offers = offers.Select(o => new OfferPair { Sku = o.sku, OfferId = o.offerId }).ToList(),
                CsvBase64 = csvBytes is null ? null : System.Convert.ToBase64String(csvBytes)
            };

            return Ok(response);
        }


        /// <summary>
        /// Inspects a specific eBay offer by its offerId.
        /// </summary>
        /// <param name="offerId">The offer ID to inspect.</param>
        /// <returns>
        /// 200 OK with offer inspection details.
        /// </returns>
        [HttpGet("offers/{offerId}")]
        public async Task<ActionResult<OfferInspect>> InspectOffer([FromRoute] string offerId)
        {
            var details = await _ebayService.InspectOfferAsync(offerId);
            return Ok(details);
        }


        /// <summary>
        /// Publishes a specific eBay offer by its offerId.
        /// </summary>
        /// <param name="offerId">The offer ID to publish.</param>
        /// <returns>
        /// 200 OK with the published offer ID.
        /// </returns>
        [HttpPost("offers/{offerId}/publish")]
        public async Task<IActionResult> PublishOffer([FromRoute] string offerId)
        {
            await _ebayService.PublishOfferAsync(offerId);
            return Ok(new { ok = true, offerId });
        }
        #endregion
    }
}