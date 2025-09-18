using eBay_API.Models.Config;
using eBay_API.Models.Controller.Buy;
using eBay_API.Models.GoogleDrive;
using eBay_API.Services;
using eBay_API.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;



namespace eBay_API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuctionController : ControllerBase
    {
        #region PROPERTIES
        private readonly ILogger<AuctionController> _logger;
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
        public AuctionController(ILogger<AuctionController> logger, IOptions<Config> options, EbayService ebayService, GoogleDriveService sheetService)
        {
            _logger = logger;
            _config = options.Value ?? throw new ArgumentNullException(nameof(options));
            _ebayService = ebayService;
            _sheetService = sheetService;
        }
        #endregion



        #region METHODS
        /// <summary>
        /// Fetches active eBay items for each configured run, filters and unifies them,
        /// and writes the results to the corresponding Google Sheet tab.
        /// </summary>
        /// <remarks>
        /// - Iterates over each run defined in configuration.
        /// - Fetches new items from eBay using queries.
        /// - Reads existing items from the Google Sheet.
        /// - Filters and merges items using filter words and time zone.
        /// - Recreates the sheet tab, deletes all rows except header, and writes the unified items.
        /// - Logs errors and returns a problem response if any operation fails.
        /// - Returns a summary of processed runs and item counts.
        /// </remarks>
        /// <returns>
        /// 200 OK with <see cref="BuyResponse"/> containing processed run results,
        /// or Problem response if an error occurs.
        /// </returns>
        [HttpPost("WriteActiveAuctionsToTable")]
        public async Task<IActionResult> WriteActiveAuctionsToTable()
        {
            var centralZone = DateTimeUtil.FindCentralTimeZone();
            var results = new List<RunResult>();

            foreach (var seller in _config.config.sellers)
            {
                _logger.LogInformation("Processing seller: {Seller}", seller);

                List<RunConfig> runs = _config.config.runs.ToList();
                var caseHits = await _sheetService.GetAllRowsAsync<CaseHit>("CASE HITS", CaseHitUtil.ToCaseHit);
                var caseHitQueries = caseHits
                    .Select(q => $"{q.Sport} {q.Name} {q.Set}".Trim() + "&limit=200&filter=price:[..10],priceCurrency:USD,buyingOptions:{AUCTION}")
                    .Select(query => QueryUtil.InjectSeller(query, seller))
                    .ToList();
                runs.Add(new RunConfig() { sheet = "CASE HITS", queries = caseHitQueries.ToArray() });

                foreach (var run in runs)
                {
                    _logger.LogInformation("Processing run: {RunName} for seller: {Seller}", run.sheet, seller);

                    var tabName = $"{run.sheet} - {seller}";

                    var sellerQueries = run.queries.Select(q => QueryUtil.InjectSeller(q, seller)).ToList();

                    var newItems = await _ebayService.FetchItemsAsync(sellerQueries);

                    List<AuctionItem> oldItems = await _sheetService.GetAllRowsAsync(tabName, AuctionItemUtil.ToAuctionItem);

                    var filterWords = (IEnumerable<string>)(_config.config.filterwords ?? Array.Empty<string>());

                    var combinedItems = AuctionItemUtil.UnifyAndFilter(newItems, oldItems, filterWords, centralZone);

                    try
                    {
                        await _sheetService.CreateTabAsync(tabName, combinedItems.First().GetHeaderRow());
                        await _sheetService.ClearAllFiltersAsync();
                        await _sheetService.DeleteAllRowsExceptHeaderAsync(tabName);
                        await _sheetService.WriteItemsAsync(
                            combinedItems,
                            tabName,
                            ai => ai.ToRow(),
                            combinedItems.First().GetHeaderRow()
                        );
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to persist items for run {RunName} and seller {Seller}", run.sheet, seller);
                        return Problem($"Failed to persist items for run '{run.sheet}' and seller '{seller}'.");
                    }

                    results.Add(new RunResult(tabName, combinedItems.Count));
                }
            }

            return Ok(results);
        }
        #endregion
    }
}



//IDEAS:
//TODO: Come up with other useful filtering methods for the Google Sheet (and make it an input param if you do)
//TODO: Add a way to filter by category so i dont get any baseball, hockey, marvel, mma, ufc, or soccer cards
//TODO: Create a method that can download a photo based on a url