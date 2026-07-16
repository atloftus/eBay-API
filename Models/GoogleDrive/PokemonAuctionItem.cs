using System.Globalization;
using eBay_API.Models.eBay.Response;



namespace eBay_API.Models.GoogleDrive
{
    public class PokemonAuctionItem : TableRow
    {
        #region PROPERTIES
        [ColumnOrder(1)]
        public string Title { get; set; } = "";

        [ColumnOrder(2)]
        public string Name { get; set; } = "trainer";

        [ColumnOrder(3)]
        public string Year { get; set; } = "";

        [ColumnOrder(4)]
        public string Generation { get; set; } = "";
        [ColumnOrder(4)]
        public string Set { get; set; } = "";

        [ColumnOrder(5)]
        public string CardNumber { get; set; } = "";

        [ColumnOrder(6)]
        public string PSA { get; set; } = "";

        [ColumnOrder(7)]
        public string HoloType { get; set; } = "";

        [ColumnOrder(8)]
        public string EndDate { get; set; } = "";
        [ColumnOrder(9)]
        public string EndTime { get; set; } = "";

        [ColumnOrder(10)]
        public string BidCount { get; set; } = "";

        [ColumnOrder(11)]
        public string Price { get; set; } = "";
                
        [ColumnOrder(12)]
        public string ItemWebUrl
        {
            get => itemWebUrl.Split('?')[0];
            set => itemWebUrl = value ?? "";
        }


        [System.Text.Json.Serialization.JsonIgnore]
        private string itemWebUrl = "";

        [System.Text.Json.Serialization.JsonIgnore]
        internal DateTime StartDateTime { get; set; }

        [System.Text.Json.Serialization.JsonIgnore]
        internal DateTime EndDateTime { get; set; }
        #endregion


        #region METHODS
        public static PokemonAuctionItem FromItemSummary(ItemSummary item, TimeZoneInfo centralZone, IEnumerable<string>? pokemonList, IEnumerable<string>? pokemonSetList)
        {
            DateTime centralEndDate = TimeZoneInfo.ConvertTimeFromUtc(item.ItemEndDate, centralZone);
            DateTime centralStartDate = TimeZoneInfo.ConvertTimeFromUtc(item.ItemCreationDate, centralZone);

            string title = AuctionItemUtil.ParseTitle(item.Title);

            //TODO: Need to add a column that has the set name
            //TODO: Need to update how PokemonAuctionItems are parsed to go in order so its easier to parse info correctly


            var result = new PokemonAuctionItem
            {
                Title = title,
                Year = AuctionItemUtil.ParseCardYear(item.Title),
                Generation = AuctionItemUtil.ParseGeneration(title),
                CardNumber = AuctionItemUtil.ParseCardNumber(title),
                Price = item.CurrentBidPrice?.Value ?? "",
                BidCount = item.BidCount?.ToString() ?? "",
                HoloType = AuctionItemUtil.ParseHoloType(title),
                EndDate = centralEndDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                EndTime = centralEndDate.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
                StartDateTime = centralStartDate,
                EndDateTime = centralEndDate,
                ItemWebUrl = AuctionItemUtil.FormatUrl(item.ItemWebUrl),
                PSA = AuctionItemUtil.ParsePSA(item.Title),
                Name = AuctionItemUtil.ParseName(title, pokemonList)
            };

            return result;
        }


        public static PokemonAuctionItem FromItemSummary(ItemSummary item, TimeZoneInfo centralZone)
            => FromItemSummary(item, centralZone, Enumerable.Empty<string>(), Enumerable.Empty<string>());
        #endregion
    }
}