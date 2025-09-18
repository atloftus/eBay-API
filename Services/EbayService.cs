using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Linq;
using eBay_API.Models.Config;
using eBay_API.Models.eBay.Response;



namespace eBay_API.Services
{
    public class EbayService
    {
        #region PROPERTIES
        private readonly EbayConfig _config;
        private string? _accessToken;
        #endregion



        #region CONSTRUCTORS
        /// <summary>
        /// Initializes a new instance of the EbayService class and retrieves the access token.
        /// </summary>
        /// <param name="config">Configuration for eBay API access.</param>
        public EbayService(EbayConfig config)
        {
            _config = config;
            _accessToken = GetAccessTokenAsync().Result;
        }
        #endregion



        #region METHODS
        /// <summary>
        /// Fetches item summaries from eBay for each query, paginating through results.
        /// </summary>
        /// <param name="queries">Search queries.</param>
        /// <returns>List of ItemSummary objects.</returns>
        public async Task<List<ItemSummary>> FetchItemsAsync(IEnumerable<string> queries)
        {
            var allItems = new List<ItemSummary>();
            using HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {_accessToken}");
            client.DefaultRequestHeaders.Add("XEBAYCMARKETPLACEID", "EBAY_US");
            client.DefaultRequestHeaders.Add("XEBAYCENDUSERCTX", "affiliateCampaignId=<ePNCampaignId>,affiliateReferenceId=<referenceId>");

            foreach (var q in queries)
            {
                string baseUrl = $"https://api.ebay.com/buy/browse/v1/item_summary/search?q={q}";
                string nextUrl = baseUrl;
                int page = 1;
                while (!string.IsNullOrEmpty(nextUrl))
                {
                    var response = await client.GetAsync(nextUrl);
                    string json = await response.Content.ReadAsStringAsync();
                    var ebayResponse = JsonSerializer.Deserialize<EbayBrowseResponse>(json);

                    if (ebayResponse?.ItemSummaries != null)
                        allItems.AddRange(ebayResponse.ItemSummaries);

                    Console.WriteLine($"Fetched page {page} for URL: {baseUrl} (items so far: {allItems.Count})");
                    nextUrl = ebayResponse?.Next;
                    page++;
                }
            }

            return allItems;
        }


        /// <summary>
        /// Retrieves buyer line items from eBay orders, paginating through results.
        /// </summary>
        /// <param name="numberOfDays">Number of days to look back.</param>
        /// <param name="limitForTesting">Optional limit for testing.</param>
        /// <returns>List of LineItem objects.</returns>
        public async Task<List<LineItem>> GetBuyerLineItemsAsync(int numberOfDays, int? limitForTesting = null)
        {
            var results = new List<LineItem>();
            var page = 1;
            var hasMore = true;
            var tradingEndpoint = IsSandbox ? "https://api.sandbox.ebay.com/ws/api.dll" : "https://api.ebay.com/ws/api.dll";
            var siteId = MarketplaceToSiteId(_config.MarketplaceId);

            while (hasMore)
            {
                var reqXml =
$@"<?xml version=""1.0"" encoding=""utf8""?>
<GetOrdersRequest xmlns=""urn:ebay:apis:eBLBaseComponents"">
  <OrderRole>Buyer</OrderRole>
  <OrderStatus>All</OrderStatus>
  <NumberOfDays>{numberOfDays}</NumberOfDays>
  <Pagination><EntriesPerPage>100</EntriesPerPage><PageNumber>{page}</PageNumber></Pagination>
</GetOrdersRequest>";

                using var http = new HttpClient();
                var msg = new HttpRequestMessage(HttpMethod.Post, tradingEndpoint)
                { Content = new StringContent(reqXml, Encoding.UTF8, "text/xml") };

                // Updated headers
                msg.Headers.Add("X-EBAY-API-COMPATIBILITY-LEVEL", "1207"); // Version
                msg.Headers.Add("X-EBAY-API-DEV-NAME", _config.ClientId);  // DevID
                msg.Headers.Add("X-EBAY-API-APP-NAME", _config.ClientId);  // AppID
                msg.Headers.Add("X-EBAY-API-CERT-NAME", _config.ClientSecret); // CertID
                msg.Headers.Add("X-EBAY-API-CALL-NAME", "GetOrders"); // CallName
                msg.Headers.Add("X-EBAY-API-SITEID", siteId); // SiteID
                msg.Headers.Add("X-EBAY-API-IAF-TOKEN", await GetAccessTokenAsync());

                using var resp = await http.SendAsync(msg);
                var xml = await resp.Content.ReadAsStringAsync();
                if (!resp.IsSuccessStatusCode)
                    throw new ApplicationException($"GetOrders failed ({(int)resp.StatusCode} {resp.StatusCode}): {xml}");

                var doc = XDocument.Parse(xml);
                var fault = doc.Descendants(XName.Get("Errors", "urn:ebay:apis:eBLBaseComponents")).FirstOrDefault();
                if (fault != null) throw new ApplicationException("Trading API error: " + fault.Value);

                XNamespace ns = "urn:ebay:apis:eBLBaseComponents";
                foreach (var txn in doc.Descendants(ns + "Transaction"))
                {
                    var item = txn.Element(ns + "Item");
                    var itemId = item?.Element(ns + "ItemID")?.Value ?? "";
                    var title = item?.Element(ns + "Title")?.Value ?? "";
                    var price = ParseDecimal(txn.Element(ns + "TransactionPrice")?.Value);
                    var created = ParseDateTime(txn.Element(ns + "CreatedDate")?.Value);

                    // Get tax and shipping amounts for each item
                    var taxAmount = ParseDecimal(txn.Element(ns + "Taxes")?.Element(ns + "TotalTaxAmount")?.Value);
                    var shippingAmount = ParseDecimal(txn.Element(ns + "ActualShippingCost")?.Value);

                    if (!string.IsNullOrEmpty(itemId))
                        results.Add(new LineItem(itemId, title, price, created, taxAmount, shippingAmount));
                }

                var hasMoreEl = doc.Descendants(ns + "HasMoreOrders").FirstOrDefault();
                hasMore = hasMoreEl != null && bool.TryParse(hasMoreEl.Value, out var hm) && hm;
                page++;

                if (limitForTesting.HasValue && results.Count >= limitForTesting.Value)
                    return results.Take(limitForTesting.Value).ToList();    
            }

            return results;
        }


        /// <summary>
        /// Upserts an inventory item in eBay using the Inventory API.
        /// </summary>
        /// <param name="sku">SKU for the item.</param>
        /// <param name="title">Title of the item.</param>
        /// <param name="condition">Condition of the item.</param>
        /// <returns>True if successful, otherwise throws exception.</returns>
        public async Task<bool> PutInventoryItemAsync(string sku, string title, string? condition)
        {
            using var http = new HttpClient();
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await GetAccessTokenAsync());
            http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var url = $"{SellBase}/sell/inventory/v1/inventory_item/{WebUtility.UrlEncode(sku)}";
            var payload = new InventoryItemPayload
            {
                availability = new Availability { shipToLocationAvailability = new ShipTo { quantity = 0 } },
                condition = condition,
                product = new Product { title = Truncate(title, 80) }
            };
            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull });

            using var req = new HttpRequestMessage(HttpMethod.Put, url)
            { Content = new StringContent(json, Encoding.UTF8, "application/json") };
            req.Content.Headers.ContentLanguage.Clear();
            req.Content.Headers.ContentLanguage.Add(_config.Locale);

            using var resp = await http.SendAsync(req);
            var text = await resp.Content.ReadAsStringAsync();
            if ((int)resp.StatusCode is >= 200 and < 300) return true;

            throw new ApplicationException($"Inventory upsert failed for {sku}: {(int)resp.StatusCode} {resp.StatusCode}  {text}");
        }


        /// <summary>
        /// Creates a draft offer for an inventory item.
        /// </summary>
        /// <param name="sku">SKU for the item.</param>
        /// <param name="categoryId">Category ID.</param>
        /// <param name="price">Price of the item.</param>
        /// <param name="currency">Currency code.</param>
        /// <returns>Offer ID string.</returns>
        public async Task<string> CreateOfferDraftAsync(string sku, string categoryId, decimal price, string currency)
        {
            using var http = new HttpClient();
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await GetAccessTokenAsync());
            http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            http.DefaultRequestHeaders.TryAddWithoutValidation("XEBAYCMARKETPLACEID", _config.MarketplaceId);

            var url = $"{SellBase}/sell/inventory/v1/offer";
            var payload = new
            {
                sku,
                marketplaceId = _config.MarketplaceId,
                format = "FIXED_PRICE",
                availableQuantity = 1,
                categoryId,
                listingPolicies = new
                {
                    paymentPolicyId = _config.PaymentPolicyId,
                    fulfillmentPolicyId = _config.FulfillmentPolicyId,
                    returnPolicyId = _config.ReturnPolicyId
                },
                pricingSummary = new
                {
                    price = new { value = price.ToString("0.00", CultureInfo.InvariantCulture), currency }
                }
            };
            var json = JsonSerializer.Serialize(payload);

            using var req = new HttpRequestMessage(HttpMethod.Post, url)
            { Content = new StringContent(json, Encoding.UTF8, "application/json") };
            req.Content.Headers.ContentLanguage.Clear();
            req.Content.Headers.ContentLanguage.Add(ContentLanguageForMarketplace(_config.MarketplaceId, _config.Locale));

            using var resp = await http.SendAsync(req);
            var text = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode)
                throw new ApplicationException($"Create offer failed for {sku}: {(int)resp.StatusCode} {resp.StatusCode}  {text}");

            using var doc = JsonDocument.Parse(text);
            var id = doc.RootElement.TryGetProperty("offerId", out var idEl) ? idEl.GetString() : "";
            return id ?? string.Empty;
        }


        /// <summary>
        /// Publishes an offer by its offer ID.
        /// </summary>
        /// <param name="offerId">Offer ID to publish.</param>
        public async Task PublishOfferAsync(string offerId)
        {
            using var http = new HttpClient();
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await GetAccessTokenAsync());
            http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var url = $"{SellBase}/sell/inventory/v1/offer/{offerId}/publish";
            using var resp = await http.PostAsync(url, content: null);
            var text = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode)
                throw new ApplicationException($"Publish failed for {offerId}: {(int)resp.StatusCode} {resp.StatusCode}  {text}");
        }


        /// <summary>
        /// Normalizes the condition string for eBay inventory.
        /// </summary>
        /// <param name="input">Input condition string.</param>
        /// <returns>Normalized condition string.</returns>
        public string NormalizeCondition(string? input)
        {
            if (_config.OmitCondition) return null!;
            if (string.IsNullOrWhiteSpace(input)) return _config.DefaultCondition;

            var val = input.Trim().ToUpperInvariant();
            if (val == "USED") val = "USED_GOOD";
            if (val == "REFURBISHED") val = "SELLER_REFURBISHED";
            return AllowedConditions.Contains(val) ? val : _config.DefaultCondition;
        }


        /// <summary>
        /// Generates a SKU for a LineItem, ensuring eBay constraints.
        /// </summary>
        /// <param name="li">LineItem object.</param>
        /// <returns>SKU string.</returns>
        public string MakeSku(LineItem li)
        {
            var safe = new string((li.Title ?? "item")
                .Where(c => char.IsLetterOrDigit(c) || c == '-')
                .ToArray());

            if (string.IsNullOrEmpty(safe)) safe = "item";
            return $"buy-{li.ItemId}-{safe[..Math.Min(16, safe.Length)].ToLowerInvariant()}";
        }


        /// <summary>
        /// Converts purchases, SKUs, and offers to CSV byte array.
        /// </summary>
        /// <param name="purchases">List of LineItem purchases.</param>
        /// <param name="createdSkus">List of created SKUs.</param>
        /// <param name="offers">List of offers.</param>
        /// <returns>CSV as byte array.</returns>
        public byte[] ToCsvBytes(List<LineItem> purchases, List<string> createdSkus, List<(string sku, string offerId)> offers)
        {
            var lines = ToCsvLines(purchases, createdSkus, offers);
            return Encoding.UTF8.GetBytes(string.Join(Environment.NewLine, lines));
        }


        /// <summary>
        /// Retrieves the access token for eBay API requests.
        /// </summary>
        /// <returns>Access token string.</returns>
        private async Task<string> GetAccessTokenAsync()
        {
            if (!string.IsNullOrEmpty(_accessToken)) return _accessToken;

            using var http = new HttpClient();
            var tokenEndpoint = IsSandbox
                ? "https://api.sandbox.ebay.com/identity/v1/oauth2/token"
                : "https://api.ebay.com/identity/v1/oauth2/token";

            var basic = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{_config.ClientId}:{_config.ClientSecret}"));
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", basic);
            http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var body =
                $"grant_type=refresh_token&refresh_token={WebUtility.UrlEncode(_config.RefreshToken)}&scope={WebUtility.UrlEncode(_config.Scope)}";
            using var req = new HttpRequestMessage(HttpMethod.Post, tokenEndpoint)
            { Content = new StringContent(body, Encoding.UTF8, "application/xwwwformurlencoded") };

            using var resp = await http.SendAsync(req);
            var text = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode)
                throw new ApplicationException($"OAuth refresh failed ({(int)resp.StatusCode} {resp.StatusCode}): {text}");

            using var doc = JsonDocument.Parse(text);
            if (!doc.RootElement.TryGetProperty("access_token", out var atEl))
                throw new ApplicationException($"OAuth refresh returned no access_token. Raw: {text}");

            _accessToken = atEl.GetString()!;
            return _accessToken;
        }


        /// <summary>
        /// Indicates if the service is using the eBay sandbox environment.
        /// </summary>
        private bool IsSandbox => string.Equals(_config.Environment, "SANDBOX", StringComparison.OrdinalIgnoreCase);


        /// <summary>
        /// Returns the base URL for eBay API requests based on environment.
        /// </summary>
        private string SellBase => IsSandbox ? "https://api.sandbox.ebay.com" : "https://api.ebay.com";


        /// <summary>
        /// Maps marketplace ID to eBay site ID.
        /// </summary>
        private static string MarketplaceToSiteId(string marketplace) => marketplace switch
        {
            "EBAY_US" => "0",
            "EBAY_GB" => "3",
            "EBAY_AU" => "15",
            "EBAY_DE" => "77",
            "EBAY_CA" => "2",
            "EBAY_FR" => "71",
            "EBAY_IT" => "101",
            "EBAY_ES" => "186",
            "EBAY_NL" => "146",
            _ => "0"
        };


        /// <summary>
        /// Maps marketplace ID to content language.
        /// </summary>
        private static string ContentLanguageForMarketplace(string marketplaceId, string defaultLocale) => marketplaceId switch
        {
            "EBAY_US" => "enUS",
            "EBAY_GB" => "enGB",
            "EBAY_AU" => "enAU",
            "EBAY_DE" => "deDE",
            "EBAY_FR" => "frFR",
            "EBAY_IT" => "itIT",
            "EBAY_ES" => "esES",
            "EBAY_CA" => "enCA",
            "EBAY_NL" => "nlNL",
            _ => defaultLocale
        };


        /// <summary>
        /// Allowed condition values for eBay inventory.
        /// </summary>
        private static readonly HashSet<string> AllowedConditions = new(StringComparer.OrdinalIgnoreCase)
    {
        "NEW","LIKE_NEW","NEW_OTHER","NEW_WITH_DEFECTS","CERTIFIED_REFURBISHED","SELLER_REFURBISHED","OPEN_BOX",
        "USED_EXCELLENT","USED_VERY_GOOD","USED_GOOD","USED_ACCEPTABLE","FOR_PARTS_OR_NOT_WORKING",
        "PRE_OWNED_EXCELLENT","PRE_OWNED_FAIR"
    };


        /// <summary>
        /// Truncates a string to the specified length.
        /// </summary>
        private static string Truncate(string? s, int len) => (s ?? "").Length <= len ? (s ?? "") : (s ?? "").Substring(0, len);


        /// <summary>
        /// Parses a string to decimal, returns null if invalid.
        /// </summary>
        private static decimal? ParseDecimal(string? v) => decimal.TryParse(v, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : (decimal?)null;


        /// <summary>
        /// Parses a string to DateTime, returns null if invalid.
        /// </summary>
        private static DateTime? ParseDateTime(string? v) => DateTime.TryParse(v, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var dt) ? dt : (DateTime?)null;


        /// <summary>
        /// Converts purchases, SKUs, and offers to CSV lines.
        /// </summary>
        private IEnumerable<string> ToCsvLines(List<LineItem> purchases, List<string> createdSkus, List<(string sku, string offerId)> offers)
        {
            yield return "ItemId,Title,Price,Created,SKU,OfferId";
            var offerMap = offers.ToDictionary(o => o.sku, o => o.offerId, StringComparer.OrdinalIgnoreCase);
            var skuSet = new HashSet<string>(createdSkus, StringComparer.OrdinalIgnoreCase);
            foreach (var li in purchases)
            {
                var sku = MakeSku(li);
                offerMap.TryGetValue(sku, out var offerId);
                yield return string.Join(",",
                    Csv(li.ItemId), Csv(Truncate(li.Title, 80)),
                    li.Price?.ToString("0.00", CultureInfo.InvariantCulture) ?? "",
                    Csv(li.Created?.ToString("u") ?? ""),
                    Csv(skuSet.Contains(sku) ? sku : ""), Csv(offerId ?? ""));
            }

            static string Csv(string? s) => "\"" + (s ?? "").Replace("\"", "\"\"") + "\"";
        }
        #endregion
    }
}