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








            // --- CASE HIT TAB LOGIC ---
            var caseHitQueries = await _sheetService.GetAllRowsAsync<CaseHit>("CASE HITS", AuctionItemUtil.ToCaseHit);

            foreach (var seller in _config.config.sellers)
            {
                _logger.LogInformation("Processing CASE HIT for seller: {Seller}", seller);

                var tabName = $"CASE HITS - {seller}";

                var sellerQueries = caseHitQueries
                    .Select(q => $"{q.Sport} {q.Name} {q.Set}".Trim() + "&limit=200&filter=price:[..10],priceCurrency:USD,buyingOptions:{AUCTION}")
                    .Select(query => InjectSeller(query, seller))
                    .ToList();

                var caseHits = await _ebayService.FetchItemsAsync(sellerQueries);

                try
                {
                    if (caseHits.Count > 0)
                    {
                        // Use the same header as AuctionItem
                        var headerRow = AuctionItem.FromItemSummary(caseHits.First(), centralZone).GetHeaderRow();
                        await _sheetService.CreateTabAsync(tabName, headerRow);
                        await _sheetService.ClearAllFiltersAsync();
                        await _sheetService.DeleteAllRowsExceptHeaderAsync(tabName);
                        await _sheetService.WriteItemsAsync(
                            caseHits.Select(item => AuctionItem.FromItemSummary(item, centralZone)).ToList(),
                            tabName,
                            ai => ai.ToRow(),
                            headerRow
                        );
                    }
                    results.Add(new RunResult(tabName, caseHits.Count));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to persist CASE HIT items for seller {Seller}", seller);
                    return Problem($"Failed to persist CASE HIT items for seller '{seller}'.");
                }
            }



















            // --- EXISTING LOGIC FOR OTHER RUNS ---
            foreach (var seller in _config.config.sellers)
            {
                _logger.LogInformation("Processing seller: {Seller}", seller);



                foreach (var run in _config.config.runs)
                {
                    _logger.LogInformation("Processing run: {RunName} for seller: {Seller}", run.sheet, seller);

                    var tabName = $"{run.sheet} - {seller}";

                    var sellerQueries = run.queries.Select(q => InjectSeller(q, seller)).ToList();

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



        private static string InjectSeller(string query, string seller)
        {
            int filterIndex = query.IndexOf("filter=");
            if (filterIndex >= 0)
            {
                int insertIndex = query.IndexOf(',', filterIndex);
                if (insertIndex >= 0)
                {
                    return query.Insert(insertIndex, $",sellers:{{{seller}}}");
                }
                else
                {
                    return query + $",sellers:{{{seller}}}";
                }
            }
            else
            {
                return query + $",sellers:{{{seller}}}";
            }
        }


        //IDEAS:
        //TODO: Come up with other useful filtering methods for the Google Sheet (and make it an input param if you do)
        //TODO: Add tracking for when I lose/win auactions and track what days I do best
        //TODO: Add a way to filter by category so i dont get any baseball, hockey, marvel, mma, ufc, or soccer cards
        //TODO: Figure out how to publish to the web and get/return the publish URL
        //TODO: Create a method that can download a photo based on a url
        //TODO: Think about expanding to COMC ($15 max shipping)..... or PSA ($6 base + $1 for each additional item).... or 4 sharp corners ($5 base + $1 for each additional item)
        //TODO: Figure out how to get a list of all your active bids (maybe hide the ones that have gotten too expensive)
        //TODO: Make functionality that lets me frequently update a sheet that shows me cards numbered to 50 or less that are ending today and still under $2... I need to get better at sniping low numbered cards for less than 2


        /// <summary>
        /// Test endpoint: applies the basic filter to the "PC" tab in the Google Sheet.
        /// </summary>
        /// <returns>200 OK if successful, or Problem if an error occurs.</returns>
        [HttpPost("Test")]
        public async Task<IActionResult> TEST()
        {
            try
            {
                //TODO: add logic to go and get all of my current bids and write them to the BIDS tab
                return Ok("Basic filter applied to PC tab.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to set basic filter for PC tab.");
                return Problem("Failed to set basic filter for PC tab.");
            }
        }


        /// <summary>
        /// Fetches and writes active bids to the "BIDS" tab in the Google Sheet.
        /// </summary>
        /// <remarks>
        /// - Retrieves active bids from eBay.
        /// - Transforms bid data into AuctionItem models.
        /// - Creates or updates the "BIDS" tab in the Google Sheet.
        /// - Writes the active bid items to the sheet, replacing any existing rows.
        /// - Returns the count of written items or an error response if the operation fails.
        /// </remarks>
        /// <returns>
        /// 200 OK with the count of active bids written,
        /// or Problem response if an error occurs.
        /// </returns>
        [HttpPost("WriteMyActiveBidsToTable")]
        public async Task<IActionResult> WriteMyActiveBidsToTable()
        {
            try
            {
                var centralZone = DateTimeUtil.FindCentralTimeZone();
                var activeBids = await _ebayService.GetActiveBidsAsync();

                var auctionItems = activeBids
                    .Select(item => AuctionItem.FromItemSummary(item, centralZone))
                    .ToList();

                if (auctionItems.Count > 0)
                {
                    await _sheetService.CreateTabAsync("BIDS", auctionItems.First().GetHeaderRow());
                    await _sheetService.DeleteAllRowsExceptHeaderAsync("BIDS");
                    await _sheetService.WriteItemsAsync(
                        auctionItems,
                        "BIDS",
                        ai => ai.ToRow(),
                        auctionItems.First().GetHeaderRow()
                    );
                }

                return Ok($"Wrote {auctionItems.Count} active bids to BIDS tab.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to write active bids to BIDS tab.");
                return Problem("Failed to write active bids to BIDS tab.");
            }
        }
        #endregion
    }
}