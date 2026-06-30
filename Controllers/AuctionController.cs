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
        [HttpPost("WriteActiveAuctionsToTable")]
        public async Task<IActionResult> WriteActiveAuctionsToTable()
        {
            var centralZone = DateTimeUtil.FindCentralTimeZone();
            var results = new List<RunResult>();

            foreach (var seller in _config.config.sellers)
            {
                _logger.LogInformation("Processing seller: {Seller}", seller);

                // Get all football and basketball cards
                var rawEbayItems = await _ebayService.FetchItemsAsync(new List<string> { QueryUtil.InjectSeller($"basketball card".Trim() + "&limit=200&filter=price:[..2],priceCurrency:USD,buyingOptions:{AUCTION}", seller), QueryUtil.InjectSeller($"football card".Trim() + "&limit=200&filter=price:[..2],priceCurrency:USD,buyingOptions:{AUCTION}", seller) });

                var filterWords = (IEnumerable<string>)(_config.config.filterwords ?? Array.Empty<string>());

                var filteredEbayItems = AuctionItemUtil.UnifyAndFilter(rawEbayItems, new List<AuctionItem>(), filterWords, centralZone);
                List<AuctionItem> selectedItems = new List<AuctionItem>();


                // 1.) Get all value 5 brands from basketball and football for this seller
                List<Brand> value5Brands = new List<Brand>();
                value5Brands.AddRange((await _sheetService.GetAllRowsAsync<Brand>(_config.googledrive.sheets.football, "Sets", BrandUtil.ToBrand)).Where(x => x.Value == 5));
                value5Brands.AddRange((await _sheetService.GetAllRowsAsync<Brand>(_config.googledrive.sheets.basketball, "Sets", BrandUtil.ToBrand)).Where(x => x.Value == 5));

                var value5BrandItems = filteredEbayItems.Where(item => value5Brands.Any(brand => item.Title.Contains(brand.Name, StringComparison.OrdinalIgnoreCase))).ToList();

                // Write these items to google drive
                await _sheetService.CreateSheetAsync(_config.googledrive.sheets.ebay, "1 - Value 5 Brands", (new AuctionItem()).GetHeaderRow());
                await _sheetService.ClearFiltersAsync(_config.googledrive.sheets.ebay, "1 - Value 5 Brands");
                await _sheetService.DeleteAllRowsExceptHeaderAsync(_config.googledrive.sheets.ebay, "1 - Value 5 Brands");
                await _sheetService.WriteItemsAsync(
                    _config.googledrive.sheets.ebay,
                    "1 - Value 5 Brands",
                    value5BrandItems,
                    ai => ai.ToRow(),
                    (new AuctionItem()).GetHeaderRow()
                );
                // Pause to avoid Google Sheets API quota limits
                await Task.Delay(30000);

                // Remove these items from filteredEbayItems
                selectedItems.AddRange(value5BrandItems);
                filteredEbayItems = filteredEbayItems.Except(value5BrandItems).ToList();


                // 2.) Get all PSA from basketball and football for this seller
                var gradedItems = filteredEbayItems.Where(item => value5Brands.Any(brand => item.Title.Contains("PSA", StringComparison.OrdinalIgnoreCase) || item.Title.Contains("BGS", StringComparison.OrdinalIgnoreCase))).ToList();

                // Write these items to google drive
                await _sheetService.CreateSheetAsync(_config.googledrive.sheets.ebay, "2 - Graded", (new AuctionItem()).GetHeaderRow());
                await _sheetService.ClearFiltersAsync(_config.googledrive.sheets.ebay, "2 - Graded");
                await _sheetService.DeleteAllRowsExceptHeaderAsync(_config.googledrive.sheets.ebay, "2 - Graded");
                await _sheetService.WriteItemsAsync(
                    _config.googledrive.sheets.ebay,
                    "2 - Graded",
                    gradedItems,
                    ai => ai.ToRow(),
                    (new AuctionItem()).GetHeaderRow()
                );
                // Pause to avoid Google Sheets API quota limits
                await Task.Delay(30000);

                // Remove these items from filteredEbayItems
                selectedItems.AddRange(gradedItems);
                filteredEbayItems = filteredEbayItems.Except(gradedItems).ToList();


                // 3.) Get all /40 or less from basketball and football for this seller
                var lowerThan40Items = filteredEbayItems.Where(x => Int32.Parse(x.OutOf) <= 40).ToList();

                // Write these items to google drive
                await _sheetService.CreateSheetAsync(_config.googledrive.sheets.ebay, "3 - Numbered <=40", (new AuctionItem()).GetHeaderRow());
                await _sheetService.ClearFiltersAsync(_config.googledrive.sheets.ebay, "3 - Numbered <=40");
                await _sheetService.DeleteAllRowsExceptHeaderAsync(_config.googledrive.sheets.ebay, "3 - Numbered <=40");
                await _sheetService.WriteItemsAsync(
                    _config.googledrive.sheets.ebay,
                    "3 - Numbered <=40",
                    lowerThan40Items,
                    ai => ai.ToRow(),
                    (new AuctionItem()).GetHeaderRow()
                );
                // Pause to avoid Google Sheets API quota limits
                await Task.Delay(30000);

                // Remove these items from filteredEbayItems
                selectedItems.AddRange(lowerThan40Items);
                filteredEbayItems = filteredEbayItems.Except(lowerThan40Items).ToList();


                // 4.) Get all rookies, patches, or autos from basketball and football for this seller
                var rpaItems = filteredEbayItems.Where(x => (x.Auto.ToLower() == "yes") && (x.Rookie.ToLower() == "yes") && (x.Patch.ToLower() == "yes")).ToList();

                // Write these items to google drive
                await _sheetService.CreateSheetAsync(_config.googledrive.sheets.ebay, "4 - RPAs", (new AuctionItem()).GetHeaderRow());
                await _sheetService.ClearFiltersAsync(_config.googledrive.sheets.ebay, "4 - RPAs");
                await _sheetService.DeleteAllRowsExceptHeaderAsync(_config.googledrive.sheets.ebay, "4 - RPAs");
                await _sheetService.WriteItemsAsync(
                    _config.googledrive.sheets.ebay,
                    "4 - RPAs",
                    rpaItems,
                    ai => ai.ToRow(),
                    (new AuctionItem()).GetHeaderRow()
                );
                // Pause to avoid Google Sheets API quota limits
                await Task.Delay(30000);

                // Remove these items from filteredEbayItems
                selectedItems.AddRange(rpaItems);
                filteredEbayItems = filteredEbayItems.Except(rpaItems).ToList();


                // 5.) Get all case hits from basketball and football for this seller
                List<CaseHit> caseHits = new List<CaseHit>();
                caseHits.AddRange((await _sheetService.GetAllRowsAsync<CaseHit>(_config.googledrive.sheets.football, "Case Hits", CaseHitUtil.ToCaseHit)).Where(x => x.Value >= 8));
                caseHits.AddRange((await _sheetService.GetAllRowsAsync<CaseHit>(_config.googledrive.sheets.basketball, "Case Hits", CaseHitUtil.ToCaseHit)).Where(x => x.Value >= 8));

                var caseHitItems = filteredEbayItems.Where(item => caseHits.Any(caseHit => item.Title.Contains(caseHit.Name, StringComparison.OrdinalIgnoreCase))).ToList();

                // Write these items to google drive
                await _sheetService.CreateSheetAsync(_config.googledrive.sheets.ebay, "5 - Case Hits", (new AuctionItem()).GetHeaderRow());
                await _sheetService.ClearFiltersAsync(_config.googledrive.sheets.ebay, "5 - Case Hits");
                await _sheetService.DeleteAllRowsExceptHeaderAsync(_config.googledrive.sheets.ebay, "5 - Case Hits");
                await _sheetService.WriteItemsAsync(
                    _config.googledrive.sheets.ebay,
                    "5 - Case Hits",
                    caseHitItems,
                    ai => ai.ToRow(),
                    (new AuctionItem()).GetHeaderRow()
                );
                // Pause to avoid Google Sheets API quota limits
                await Task.Delay(30000);

                // Remove these items from filteredEbayItems
                selectedItems.AddRange(caseHitItems);
                filteredEbayItems = filteredEbayItems.Except(caseHitItems).ToList();


                // 6.) Get all GOATS from basketball and football for this seller
                List<Player> goats = new List<Player>();
                goats.AddRange((await _sheetService.GetAllRowsAsync<Player>(_config.googledrive.sheets.football, "Players", PlayerUtil.ToPlayer)).Where(x => x.CollectionArea.ToLower() == "goats"));
                goats.AddRange((await _sheetService.GetAllRowsAsync<Player>(_config.googledrive.sheets.basketball, "Players", PlayerUtil.ToPlayer)).Where(x => x.CollectionArea.ToLower() == "goats"));

                var goatItems = filteredEbayItems.Where(item => goats.Any(goats => item.Title.Contains(goats.Name, StringComparison.OrdinalIgnoreCase))).ToList();

                // Write these items to google drive
                await _sheetService.CreateSheetAsync(_config.googledrive.sheets.ebay, "6 - GOATS", (new AuctionItem()).GetHeaderRow());
                await _sheetService.ClearFiltersAsync(_config.googledrive.sheets.ebay, "6 - GOATS");
                await _sheetService.DeleteAllRowsExceptHeaderAsync(_config.googledrive.sheets.ebay, "6 - GOATS");
                await _sheetService.WriteItemsAsync(
                    _config.googledrive.sheets.ebay,
                    "6 - GOATS",
                    goatItems,
                    ai => ai.ToRow(),
                    (new AuctionItem()).GetHeaderRow()
                );
                // Pause to avoid Google Sheets API quota limits
                await Task.Delay(30000);

                // Remove these items from filteredEbayItems
                selectedItems.AddRange(goatItems);
                filteredEbayItems = filteredEbayItems.Except(goatItems).ToList();


                // 7.) Get all PC from basketball and football for this seller
                List<Player> pc = new List<Player>();
                pc.AddRange((await _sheetService.GetAllRowsAsync<Player>(_config.googledrive.sheets.football, "Players", PlayerUtil.ToPlayer)).Where(x => x.CollectionArea.ToLower() == "pc"));
                pc.AddRange((await _sheetService.GetAllRowsAsync<Player>(_config.googledrive.sheets.basketball, "Players", PlayerUtil.ToPlayer)).Where(x => x.CollectionArea.ToLower() == "pc"));

                var pcItems = filteredEbayItems.Where(item => pc.Any(pc => item.Title.Contains(pc.Name, StringComparison.OrdinalIgnoreCase))).ToList();

                // Write these items to google drive
                await _sheetService.CreateSheetAsync(_config.googledrive.sheets.ebay, "7 - PC", (new AuctionItem()).GetHeaderRow());
                await _sheetService.ClearFiltersAsync(_config.googledrive.sheets.ebay, "7 - PC");
                await _sheetService.DeleteAllRowsExceptHeaderAsync(_config.googledrive.sheets.ebay, "7 - PC");
                await _sheetService.WriteItemsAsync(
                    _config.googledrive.sheets.ebay,
                    "7 - PC",
                    pcItems,
                    ai => ai.ToRow(),
                    (new AuctionItem()).GetHeaderRow()
                );
                // Pause to avoid Google Sheets API quota limits
                await Task.Delay(30000);

                // Remove these items from filteredEbayItems
                selectedItems.AddRange(pcItems);
                filteredEbayItems = filteredEbayItems.Except(pcItems).ToList();


                // 8.) Get all stars from basketball and football for this seller
                List<Player> stars = new List<Player>();
                stars.AddRange((await _sheetService.GetAllRowsAsync<Player>(_config.googledrive.sheets.football, "Players", PlayerUtil.ToPlayer)).Where(x => x.CollectionArea.ToLower() == "stars"));
                stars.AddRange((await _sheetService.GetAllRowsAsync<Player>(_config.googledrive.sheets.basketball, "Players", PlayerUtil.ToPlayer)).Where(x => x.CollectionArea.ToLower() == "stars"));

                var starItems = filteredEbayItems.Where(item => stars.Any(pc => item.Title.Contains(pc.Name, StringComparison.OrdinalIgnoreCase))).ToList();

                // Write these items to google drive
                await _sheetService.CreateSheetAsync(_config.googledrive.sheets.ebay, "8 - Stars", (new AuctionItem()).GetHeaderRow());
                await _sheetService.ClearFiltersAsync(_config.googledrive.sheets.ebay, "8 - Stars");
                await _sheetService.DeleteAllRowsExceptHeaderAsync(_config.googledrive.sheets.ebay, "8 - Stars");
                await _sheetService.WriteItemsAsync(
                    _config.googledrive.sheets.ebay,
                    "8 - Stars",
                    starItems,
                    ai => ai.ToRow(),
                    (new AuctionItem()).GetHeaderRow()
                );
                // Pause to avoid Google Sheets API quota limits
                await Task.Delay(30000);

                // Remove these items from filteredEbayItems
                selectedItems.AddRange(starItems);
                filteredEbayItems = filteredEbayItems.Except(starItems).ToList();


                // 9.) Get all level 4 brands from basketball and football for this seller
                List<Brand> value4Brands = new List<Brand>();
                value4Brands.AddRange((await _sheetService.GetAllRowsAsync<Brand>(_config.googledrive.sheets.football, "Sets", BrandUtil.ToBrand)).Where(x => x.Value == 4));
                value4Brands.AddRange((await _sheetService.GetAllRowsAsync<Brand>(_config.googledrive.sheets.basketball, "Sets", BrandUtil.ToBrand)).Where(x => x.Value == 4));

                var value4BrandItems = filteredEbayItems.Where(item => value4Brands.Any(brand => item.Title.Contains(brand.Name, StringComparison.OrdinalIgnoreCase))).ToList().Where(x => Int32.Parse(x.OutOf) <= 1000).ToList();

                // Write these items to google drive
                await _sheetService.CreateSheetAsync(_config.googledrive.sheets.ebay, "9 - Value 4 Brands", (new AuctionItem()).GetHeaderRow());
                await _sheetService.ClearFiltersAsync(_config.googledrive.sheets.ebay, "9 - Value 4 Brands");
                await _sheetService.DeleteAllRowsExceptHeaderAsync(_config.googledrive.sheets.ebay, "9 - Value 4 Brands");
                await _sheetService.WriteItemsAsync(
                    _config.googledrive.sheets.ebay,
                    "9 - Value 4 Brands",
                    value4BrandItems,
                    ai => ai.ToRow(),
                    (new AuctionItem()).GetHeaderRow()
                );
                // Pause to avoid Google Sheets API quota limits
                await Task.Delay(30000);

                // Remove these items from filteredEbayItems
                selectedItems.AddRange(value4BrandItems);
                filteredEbayItems = filteredEbayItems.Except(value4BrandItems).ToList();


                // 10.) Get all /100 or less from basketball and football for this seller
                var lowerThan100Items = filteredEbayItems.Where(x => Int32.Parse(x.OutOf) <= 100).ToList();

                // Write these items to google drive
                await _sheetService.CreateSheetAsync(_config.googledrive.sheets.ebay, "10 - Numbered <=100", (new AuctionItem()).GetHeaderRow());
                await _sheetService.ClearFiltersAsync(_config.googledrive.sheets.ebay, "10 - Numbered <=100");
                await _sheetService.DeleteAllRowsExceptHeaderAsync(_config.googledrive.sheets.ebay, "10 - Numbered <=100");
                await _sheetService.WriteItemsAsync(
                    _config.googledrive.sheets.ebay,
                    "10 - Numbered <=100",
                    lowerThan100Items,
                    ai => ai.ToRow(),
                    (new AuctionItem()).GetHeaderRow()
                );
                // Pause to avoid Google Sheets API quota limits
                await Task.Delay(30000);

                // Remove these items from filteredEbayItems
                selectedItems.AddRange(lowerThan100Items);
                filteredEbayItems = filteredEbayItems.Except(lowerThan100Items).ToList();


                // 11.) Get all remaining cards
                await _sheetService.CreateSheetAsync(_config.googledrive.sheets.ebay, "11 - Remaining", (new AuctionItem()).GetHeaderRow());
                await _sheetService.ClearFiltersAsync(_config.googledrive.sheets.ebay, "11 - Remaining");
                await _sheetService.DeleteAllRowsExceptHeaderAsync(_config.googledrive.sheets.ebay, "11 - Remaining");
                await _sheetService.WriteItemsAsync(
                    _config.googledrive.sheets.ebay,
                    "11 - Remaining",
                    filteredEbayItems,
                    ai => ai.ToRow(),
                    (new AuctionItem()).GetHeaderRow()
                );
                // Pause to avoid Google Sheets API quota limits
                await Task.Delay(30000);


                // 12.) Get all selected cards
                await _sheetService.CreateSheetAsync(_config.googledrive.sheets.ebay, "12 - Selected", (new AuctionItem()).GetHeaderRow());
                await _sheetService.ClearFiltersAsync(_config.googledrive.sheets.ebay, "12 - Selected");
                await _sheetService.DeleteAllRowsExceptHeaderAsync(_config.googledrive.sheets.ebay, "12 - Selected");
                await _sheetService.WriteItemsAsync(
                    _config.googledrive.sheets.ebay,
                    "12 - Selected",
                    selectedItems,
                    ai => ai.ToRow(),
                    (new AuctionItem()).GetHeaderRow()
                );
                // Final pause to avoid Google Sheets API quota limits
                await Task.Delay(30000);
            }

            return Ok(results);
        }
        #endregion
    }
}