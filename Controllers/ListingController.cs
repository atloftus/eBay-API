using eBay_API.Models.Config;
using eBay_API.Models.Controller.Buy;
using eBay_API.Models.GoogleDrive;
using eBay_API.Services;
using eBay_API.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Collections.Generic;
using System.Linq;

namespace eBay_API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ListingController : ControllerBase
    {
        #region PROPERTIES
        private readonly ILogger<ListingController> _logger;
        private readonly Config _config;
        private readonly EbayService _ebayService;
        private readonly GoogleDriveService _sheetService;
        #endregion



        #region CONSTRUCTORS
        /// <summary>
        /// Initializes a new instance of the <see cref="AuctionController"/> class.
        /// </summary>
        /// <param name="logger">Logger for diagnostic output.</param>
        /// <param name="options">Configuration options injected from app settings.</param>
        /// <param name="ebayService">Service for eBay API operations.</param>
        /// <param name="sheetService">Service for Google Sheets operations.</param>
        /// <exception cref="ArgumentNullException">Thrown if options is null.</exception>
        public ListingController(ILogger<ListingController> logger, IOptions<Config> options, EbayService ebayService, GoogleDriveService sheetService)
        {
            _logger = logger;
            _config = options.Value ?? throw new ArgumentNullException(nameof(options));
            _ebayService = ebayService;
            _sheetService = sheetService;
        }
        #endregion



        #region METHODS
        [HttpPost("ListAnItem_TEST")]
        public async Task<IActionResult> ListAnItem_TEST()
        {
            // Create a minimal, static test item using the EbayService helper methods.
            // This follows the project's inventory -> offer -> publish flow:
            // 1) Put inventory item
            // 2) Create an offer draft for that SKU
            // 3) Publish the offer

            try
            {
                // Generate a deterministic-but-unique SKU for testing
                var sku = $"TEST-SKU-{Guid.NewGuid():N}".Substring(0, 24); // keep SKU length reasonable

                // Static test title
                var title = "TEST ITEM - Do Not Purchase (Automated Test Listing)";

                // Determine condition according to config and service normalization
                string? condition = null;
                if (!(_config?.ebay?.OmitCondition ?? false))
                {
                    condition = _ebayService.NormalizeCondition(_config?.ebay?.DefaultCondition);
                }

                // Upsert inventory item
                await _ebayService.PutInventoryItemAsync(sku, title, condition);

                // Use fallback category and price from config when available
                var categoryId = _config?.ebay?.CategoryFallback ?? "0";
                var price = _config?.ebay?.FallbackPrice ?? 9.99m;

                // Default currency; keep simple for tests. Adjust if you need locale-based mapping.
                var currency = "USD";

                // Create offer draft and then publish
                var offerId = await _ebayService.CreateOfferDraftAsync(sku, categoryId, price, currency);
                await _ebayService.PublishOfferAsync(offerId);

                return Ok(new
                {
                    Message = "Test item listed",
                    Sku = sku,
                    OfferId = offerId,
                    Price = price,
                    Currency = currency,
                    CategoryId = categoryId,
                    Condition = condition
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ListAnItem_TEST failed");
                return StatusCode(500, new { Error = "Failed to create test listing", Details = ex.Message });
            }
        }
        #endregion
    }
}
