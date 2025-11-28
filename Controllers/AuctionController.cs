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
        [HttpPost("WriteActiveAuctionsToTableW1")]
        public async Task<IActionResult> WriteActiveAuctionsToTableW1()
        {
            var centralZone = DateTimeUtil.FindCentralTimeZone();
            var results = new List<RunResult>();

            foreach (var seller in _config.config.sellers)
            {
                _logger.LogInformation("Processing seller: {Seller}", seller);

                List<RunConfig> runs = new List<RunConfig>();

                // 1.) Get all level 5 brands from basketball and football for this seller
                List<string> queries1 = new List<string>();
                var topTierBasketballBrands = await _sheetService.GetAllRowsAsync<Brand>(_config.googledrive.sheets.basketball, "Brands", BrandUtil.ToBrand);
                queries1.AddRange(topTierBasketballBrands.Where(b => b.Value == 5)
                    .Select(q => $"(basketball) ({q.Manufacturer} {q.Name})".Trim() + "&limit=200&filter=price:[..10],priceCurrency:USD,buyingOptions:{AUCTION}")
                    .Select(query => QueryUtil.InjectSeller(query, seller))
                    .ToList());

                var topTierFootballBrands = await _sheetService.GetAllRowsAsync<Brand>(_config.googledrive.sheets.football, "Brands", BrandUtil.ToBrand);
                queries1.AddRange(topTierFootballBrands.Where(b => b.Value == 5)
                    .Select(q => $"(football) ({q.Manufacturer} {q.Name})".Trim() + "&limit=200&filter=price:[..10],priceCurrency:USD,buyingOptions:{AUCTION}")
                    .Select(query => QueryUtil.InjectSeller(query, seller))
                    .ToList());

                runs.Add(new RunConfig() { sheet = $"1 - Top Tier Brands - {seller}", queries = queries1.ToArray() });


                // 2.) Get all PSA from basketball and football for this seller
                runs.Add(new RunConfig() { sheet = $"2 - PSA - {seller}", queries = (new List<string> { QueryUtil.InjectSeller($"basketball card PSA".Trim() + "&limit=200&filter=price:[..10],priceCurrency:USD,buyingOptions:{AUCTION}", seller), QueryUtil.InjectSeller($"football card PSA".Trim() + "&limit=200&filter=price:[..10],priceCurrency:USD,buyingOptions:{AUCTION}", seller) }).ToArray() });


                //TODO: Need to fix this section
                //// 3.) Get all case hits from basketball and football for this seller
                //List<string> queries3 = new List<string>();
                //var caseHitsBasketball = await _sheetService.GetAllRowsAsync<CaseHit>(_config.googledrive.sheets.basketball, "Case Hits", CaseHitUtil.ToCaseHit);
                //queries3.AddRange(caseHitsBasketball
                //    .Select(q => $"(basketball) ({q.Set} {q.Name})".Trim() + "&limit=200&filter=price:[..10],priceCurrency:USD,buyingOptions:{AUCTION}")
                //    .Select(query => QueryUtil.InjectSeller(query, seller))
                //    .ToList());

                //var caseHitsFootball = await _sheetService.GetAllRowsAsync<CaseHit>(_config.googledrive.sheets.football, "Case Hits", CaseHitUtil.ToCaseHit);
                //queries3.AddRange(caseHitsFootball
                //    .Select(q => $"(football) ({q.Set} {q.Name})".Trim() + "&limit=200&filter=price:[..10],priceCurrency:USD,buyingOptions:{AUCTION}")
                //    .Select(query => QueryUtil.InjectSeller(query, seller))
                //    .ToList());

                //runs.Add(new RunConfig() { sheet = $"3 - Case Hits - {seller}", queries = queries3.ToArray() });


                foreach (var run in runs)
                {
                    _logger.LogInformation("Processing run: {RunName} for seller: {Seller}", run.sheet, seller);

                    var sheetName = $"{run.sheet} - {seller}";

                    var sellerQueries = run.queries.Select(q => QueryUtil.InjectSeller(q, seller)).ToList();

                    var newItems = await _ebayService.FetchItemsAsync(sellerQueries);

                    List<AuctionItem> oldItems = await _sheetService.GetAllRowsAsync(_config.googledrive.sheets.ebay, sheetName, AuctionItemUtil.ToAuctionItem);

                    var filterWords = (IEnumerable<string>)(_config.config.filterwords ?? Array.Empty<string>());

                    var combinedItems = AuctionItemUtil.UnifyAndFilter(newItems, oldItems, filterWords, centralZone);

                    try
                    {
                        await _sheetService.CreateSheetAsync(_config.googledrive.sheets.ebay, sheetName, combinedItems.First().GetHeaderRow());
                        await _sheetService.ClearAllFiltersAsync(_config.googledrive.sheets.ebay);
                        await _sheetService.DeleteAllRowsExceptHeaderAsync(_config.googledrive.sheets.ebay, sheetName);
                        await _sheetService.WriteItemsAsync(
                            _config.googledrive.sheets.ebay,
                            sheetName,
                            combinedItems,
                            ai => ai.ToRow(),
                            combinedItems.First().GetHeaderRow()
                        );
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to persist items for run {RunName} and seller {Seller}", run.sheet, seller);
                        return Problem($"Failed to persist items for run '{run.sheet}' and seller '{seller}'.");
                    }

                    results.Add(new RunResult(sheetName, combinedItems.Count));
                }
            }

            return Ok(results);
        }



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
        [HttpPost("WriteActiveAuctionsToTableW2")]
        public async Task<IActionResult> WriteActiveAuctionsToTableW2()
        {
            var centralZone = DateTimeUtil.FindCentralTimeZone();
            var results = new List<RunResult>();

            foreach (var seller in _config.config.sellers)
            {
                _logger.LogInformation("Processing seller: {Seller}", seller);

                List<RunConfig> runs = new List<RunConfig>();

                // 4.) Get all GOATS from basketball and football for this seller
                List<string> queries4 = new List<string>();
                var goatsBasketball = await _sheetService.GetAllRowsAsync<Player>(_config.googledrive.sheets.basketball, "Players", PlayerUtil.ToPlayer);
                queries4.AddRange(goatsBasketball.Where(b => b.GOAT.ToLower() == "yes")
                    .Select(q => $"(basketball) ({q.Name})".Trim() + "&limit=200&filter=price:[..10],priceCurrency:USD,buyingOptions:{AUCTION}")
                    .Select(query => QueryUtil.InjectSeller(query, seller))
                    .ToList());

                var goatsFootball = await _sheetService.GetAllRowsAsync<Player>(_config.googledrive.sheets.football, "Players", PlayerUtil.ToPlayer);
                queries4.AddRange(goatsFootball.Where(b => b.GOAT.ToLower() == "yes")
                    .Select(q => $"(football) ({q.Name})".Trim() + "&limit=200&filter=price:[..10],priceCurrency:USD,buyingOptions:{AUCTION}")
                    .Select(query => QueryUtil.InjectSeller(query, seller))
                    .ToList());

                runs.Add(new RunConfig() { sheet = $"4 - GOATS - {seller}", queries = queries4.ToArray() });


                //5.) Get all PC from basketball and football for this seller
                List<string> queries5 = new List<string>();
                var pcBasketball = await _sheetService.GetAllRowsAsync<Player>(_config.googledrive.sheets.basketball, "Players", PlayerUtil.ToPlayer);
                queries5.AddRange(pcBasketball.Where(b => b.CollectionArea.ToLower() == "pc")
                    .Select(q => $"(basketball) ({q.Name})".Trim() + "&limit=200&filter=price:[..10],priceCurrency:USD,buyingOptions:{AUCTION}")
                    .Select(query => QueryUtil.InjectSeller(query, seller))
                    .ToList());

                var pcFootball = await _sheetService.GetAllRowsAsync<Player>(_config.googledrive.sheets.football, "Players", PlayerUtil.ToPlayer);
                queries5.AddRange(pcFootball.Where(b => b.CollectionArea.ToLower() == "pc")
                    .Select(q => $"(football) ({q.Name})".Trim() + "&limit=200&filter=price:[..10],priceCurrency:USD,buyingOptions:{AUCTION}")
                    .Select(query => QueryUtil.InjectSeller(query, seller))
                    .ToList());

                runs.Add(new RunConfig() { sheet = $"5 - PC - {seller}", queries = queries5.ToArray() });



                // 6.) Get all numbered stars from basketball and football for this seller
                List<string> queries6 = new List<string>();
                var starsBasketball = await _sheetService.GetAllRowsAsync<Player>(_config.googledrive.sheets.basketball, "Players", PlayerUtil.ToPlayer);
                queries6.AddRange(starsBasketball.Where(b => b.CollectionArea.ToLower() == "stars")
                   .Select(q => $"(basketball) ({q.Name})".Trim() + "&limit=200&filter=price:[..10],priceCurrency:USD,buyingOptions:{AUCTION}")
                   .Select(query => QueryUtil.InjectSeller(query, seller))
                   .ToList());

                var starsFootball = await _sheetService.GetAllRowsAsync<Player>(_config.googledrive.sheets.football, "Players", PlayerUtil.ToPlayer);
                queries6.AddRange(starsFootball.Where(b => b.CollectionArea.ToLower() == "stars")
                   .Select(q => $"(football) ({q.Name})".Trim() + "&limit=200&filter=price:[..10],priceCurrency:USD,buyingOptions:{AUCTION}")
                   .Select(query => QueryUtil.InjectSeller(query, seller))
                   .ToList());

                runs.Add(new RunConfig() { sheet = $"6 - Stars - {seller}", queries = queries6.ToArray() });


                foreach (var run in runs)
                {
                    _logger.LogInformation("Processing run: {RunName} for seller: {Seller}", run.sheet, seller);

                    var sheetName = $"{run.sheet} - {seller}";

                    var sellerQueries = run.queries.Select(q => QueryUtil.InjectSeller(q, seller)).ToList();

                    var newItems = await _ebayService.FetchItemsAsync(sellerQueries);

                    List<AuctionItem> oldItems = await _sheetService.GetAllRowsAsync(_config.googledrive.sheets.ebay, sheetName, AuctionItemUtil.ToAuctionItem);

                    var filterWords = (IEnumerable<string>)(_config.config.filterwords ?? Array.Empty<string>());

                    var combinedItems = AuctionItemUtil.UnifyAndFilter(newItems, oldItems, filterWords, centralZone);

                    try
                    {
                        await _sheetService.CreateSheetAsync(_config.googledrive.sheets.ebay, sheetName, combinedItems.First().GetHeaderRow());
                        await _sheetService.ClearAllFiltersAsync(_config.googledrive.sheets.ebay);
                        await _sheetService.DeleteAllRowsExceptHeaderAsync(_config.googledrive.sheets.ebay, sheetName);
                        await _sheetService.WriteItemsAsync(
                            _config.googledrive.sheets.ebay,
                            sheetName,
                            combinedItems,
                            ai => ai.ToRow(),
                            combinedItems.First().GetHeaderRow()
                        );
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to persist items for run {RunName} and seller {Seller}", run.sheet, seller);
                        return Problem($"Failed to persist items for run '{run.sheet}' and seller '{seller}'.");
                    }

                    results.Add(new RunResult(sheetName, combinedItems.Count));
                }
            }

            return Ok(results);
        }



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
        [HttpPost("WriteActiveAuctionsToTableW3")]
        public async Task<IActionResult> WriteActiveAuctionsToTableW3()
        {
            var centralZone = DateTimeUtil.FindCentralTimeZone();
            var results = new List<RunResult>();

            foreach (var seller in _config.config.sellers)
            {
                _logger.LogInformation("Processing seller: {Seller}", seller);

                List<RunConfig> runs = new List<RunConfig>();

                //7.A) Get all numbered cards from basketball and football for this seller
                runs.Add(new RunConfig() { sheet = $"7 - Numbered - {seller}", queries = (new List<string> { QueryUtil.InjectSeller($"basketball card /".Trim() + "&limit=200&filter=price:[..10],priceCurrency:USD,buyingOptions:{AUCTION}", seller), QueryUtil.InjectSeller($"football card /".Trim() + "&limit=200&filter=price:[..10],priceCurrency:USD,buyingOptions:{AUCTION}", seller) }).ToArray() });


                foreach (var run in runs)
                {
                    _logger.LogInformation("Processing run: {RunName} for seller: {Seller}", run.sheet, seller);

                    var sheetName = $"{run.sheet} - {seller}";

                    var sellerQueries = run.queries.Select(q => QueryUtil.InjectSeller(q, seller)).ToList();

                    var newItems = await _ebayService.FetchItemsAsync(sellerQueries);

                    List<AuctionItem> oldItems = await _sheetService.GetAllRowsAsync(_config.googledrive.sheets.ebay, sheetName, AuctionItemUtil.ToAuctionItem);

                    var filterWords = (IEnumerable<string>)(_config.config.filterwords ?? Array.Empty<string>());

                    var combinedItems = AuctionItemUtil.UnifyAndFilter(newItems, oldItems, filterWords, centralZone);

                    try
                    {
                        if (run.sheet.Contains("Numbered"))
                        {
                            // 7.) Get all /40 or less from basketball and football for this seller
                            await _sheetService.CreateSheetAsync(_config.googledrive.sheets.ebay, $"7 - Numbered <=40 - {seller}", combinedItems.First().GetHeaderRow());
                            await _sheetService.ClearAllFiltersAsync(_config.googledrive.sheets.ebay);
                            await _sheetService.DeleteAllRowsExceptHeaderAsync(_config.googledrive.sheets.ebay, sheetName);
                            var items40OrLess = combinedItems.Where(x => Int32.Parse(x.OutOf) <= 40).ToList();
                            await _sheetService.WriteItemsAsync(
                                _config.googledrive.sheets.ebay,
                                $"7 - Numbered <=40 - {seller}",
                                items40OrLess,
                                ai => ai.ToRow(),
                                combinedItems.First().GetHeaderRow()
                            );


                            // 8.) Get all rookies, patches, or autos from basketball and football for this seller
                            await _sheetService.CreateSheetAsync(_config.googledrive.sheets.ebay, $"8 - RPAs - {seller}", combinedItems.First().GetHeaderRow());
                            await _sheetService.ClearAllFiltersAsync(_config.googledrive.sheets.ebay);
                            await _sheetService.DeleteAllRowsExceptHeaderAsync(_config.googledrive.sheets.ebay, sheetName);
                            var itemsRPAs = combinedItems.Where(x => (x.Auto.ToLower() == "yes") && (x.Rookie.ToLower() == "yes") && (x.Patch.ToLower() == "yes")).ToList();
                            await _sheetService.WriteItemsAsync(
                                _config.googledrive.sheets.ebay,
                                $"8 - RPAs - {seller}",
                                itemsRPAs,
                                ai => ai.ToRow(),
                                combinedItems.First().GetHeaderRow()
                            );


                            // 9.) Get all level 4 brands from basketball and football for this seller
                            await _sheetService.CreateSheetAsync(_config.googledrive.sheets.ebay, $"9 - Mid-High Tier Brands - {seller}", combinedItems.First().GetHeaderRow());
                            await _sheetService.ClearAllFiltersAsync(_config.googledrive.sheets.ebay);
                            await _sheetService.DeleteAllRowsExceptHeaderAsync(_config.googledrive.sheets.ebay, sheetName);
                            var itemsLevel4 = new List<AuctionItem>();

                            List<string> level4Brands = new List<string>();
                            var midTopTierBasketballBrands = await _sheetService.GetAllRowsAsync<Brand>(_config.googledrive.sheets.basketball, "Brands", BrandUtil.ToBrand);
                            level4Brands.AddRange(midTopTierBasketballBrands.Where(b => b.Value == 4).Select(a => a.Name).ToList());

                            var midTopTierFootballBrands = await _sheetService.GetAllRowsAsync<Brand>(_config.googledrive.sheets.football, "Brands", BrandUtil.ToBrand);
                            level4Brands.AddRange(midTopTierFootballBrands.Where(b => b.Value == 4).Select(a => a.Name).ToList());

                            itemsLevel4.AddRange(combinedItems.Where(item => level4Brands.Distinct().Any(brand => item.Title.Contains(brand, StringComparison.OrdinalIgnoreCase))).ToList());

                            await _sheetService.WriteItemsAsync(
                                _config.googledrive.sheets.ebay,
                                $"9 - Mid-High Tier Brands - {seller}",
                                itemsLevel4,
                                ai => ai.ToRow(),
                                combinedItems.First().GetHeaderRow()
                            );


                            // 10.) Get all /100 or less from basketball and football for this seller
                            await _sheetService.CreateSheetAsync(_config.googledrive.sheets.ebay, $"10 - Numbered <= 100 - {seller}", combinedItems.First().GetHeaderRow());
                            await _sheetService.ClearAllFiltersAsync(_config.googledrive.sheets.ebay);
                            await _sheetService.DeleteAllRowsExceptHeaderAsync(_config.googledrive.sheets.ebay, sheetName);
                            var items100OrLess = combinedItems.Where(x => (Int32.Parse(x.OutOf) > 40) && (Int32.Parse(x.OutOf) <= 100)).ToList();
                            await _sheetService.WriteItemsAsync(
                                _config.googledrive.sheets.ebay,
                                $"10 - Numbered <= 100 - {seller}",
                                items100OrLess,
                                ai => ai.ToRow(),
                                combinedItems.First().GetHeaderRow()
                            );


                            // 11.) Get all remaining numbered cards over 100 from basketball and football for this seller
                            var remainingItems = combinedItems.Except(items40OrLess).Except(itemsRPAs).Except(itemsLevel4).Except(items100OrLess).ToList();
                            await _sheetService.CreateSheetAsync(_config.googledrive.sheets.ebay, $"11 - Numbered > 100 - {seller}", combinedItems.First().GetHeaderRow());
                            await _sheetService.ClearAllFiltersAsync(_config.googledrive.sheets.ebay);
                            await _sheetService.DeleteAllRowsExceptHeaderAsync(_config.googledrive.sheets.ebay, sheetName);
                            await _sheetService.WriteItemsAsync(
                               _config.googledrive.sheets.ebay,
                               $"11 - Numbered > 100 - {seller}",
                               remainingItems,
                               ai => ai.ToRow(),
                               combinedItems.First().GetHeaderRow()
                           );


                            //IDEAS:
                            //TODO: Figure out a way to get my set collection into this mix
                            //TODO: Add a way to look for old cards (pre 1980)
                            //TODO: Get a way to look for specific Rookie cards
                        }
                        else
                        {
                            await _sheetService.CreateSheetAsync(_config.googledrive.sheets.ebay, sheetName, combinedItems.First().GetHeaderRow());
                            await _sheetService.ClearAllFiltersAsync(_config.googledrive.sheets.ebay);
                            await _sheetService.DeleteAllRowsExceptHeaderAsync(_config.googledrive.sheets.ebay, sheetName);
                            await _sheetService.WriteItemsAsync(
                                _config.googledrive.sheets.ebay,
                                sheetName,
                                combinedItems,
                                ai => ai.ToRow(),
                                combinedItems.First().GetHeaderRow()
                            );
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to persist items for run {RunName} and seller {Seller}", run.sheet, seller);
                        return Problem($"Failed to persist items for run '{run.sheet}' and seller '{seller}'.");
                    }

                    results.Add(new RunResult(sheetName, combinedItems.Count));
                }
            }

            return Ok(results);
        }
        #endregion
    }
}