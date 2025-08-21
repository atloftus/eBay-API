using System.Globalization;
using eBay_API.Models.eBay.Response;



namespace eBay_API.Models.GoogleDrive
{
    public class AuctionItem : TableRow
    {
        #region PROPERTIES
        [ColumnOrder(1)]
        public string Title { get; set; }
        [ColumnOrder(2)]
        public string Year { get; set; }
        [ColumnOrder(3)]
        public string Price { get; set; }
        [ColumnOrder(4)]
        public string BidCount { get; set; }
        [ColumnOrder(5)]
        public string EndDate { get; set; }
        [ColumnOrder(6)]
        public string EndTime { get; set; }
        [ColumnOrder(7)]
        public string Numbered { get; set; }
        [ColumnOrder(8)]
        public string OutOf { get; set; }
        [ColumnOrder(9)]
        public string Rookie { get; set; }
        [ColumnOrder(10)]
        public string ItemWebUrl { get; set; }
        [ColumnOrder(11)]
        public string PSA { get; set; }
        #endregion



        #region METHODS
        /// <summary>
        /// Converts an ItemSummary to an AuctionItem, normalizing dates and formatting fields for Google Sheets.
        /// </summary>
        /// <param name="item">The ItemSummary to convert.</param>
        /// <param name="centralZone">Time zone for date conversion.</param>
        /// <returns>A new AuctionItem instance.</returns>
        public static AuctionItem FromItemSummary(ItemSummary item, TimeZoneInfo centralZone)
        {
            DateTime centralEndDate = TimeZoneInfo.ConvertTimeFromUtc(item.ItemEndDate, centralZone);

            return new AuctionItem
            {
                Title = AuctionItemUtil.ParseTitle(item.Title),
                Year = GoogleDriveService.ParseCardYear(item.Title),
                Price = item.CurrentBidPrice?.Value ?? "",
                BidCount = item.BidCount?.ToString() ?? "",
                EndDate = centralEndDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                EndTime = centralEndDate.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
                Numbered = AuctionItemUtil.ParseIsNumbered(item.Title) ? "Yes" : "No",
                OutOf = AuctionItemUtil.ParseOutOf(item.Title).ToString(),
                Rookie = AuctionItemUtil.ParseRC(item.Title),
                ItemWebUrl = AuctionItemUtil.FormatUrl(item.ItemWebUrl),
                PSA = AuctionItemUtil.ParsePSA(item.Title)
            };
        }
        #endregion
    }
}