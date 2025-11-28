namespace eBay_API.Models.GoogleDrive
{
    public class Player : TableRow
    {
        #region PROPERTIES
        [ColumnOrder(1)]
        public string Name { get; set; }

        [ColumnOrder(2)]
        public string Position { get; set; }

        [ColumnOrder(3)]
        public int CollectionRCYear { get; set; }

        [ColumnOrder(4)]
        public int StartYear { get; set; }

        [ColumnOrder(5)]
        public int EndYear { get; set; }

        [ColumnOrder(6)]
        public string Status { get; set; }

        [ColumnOrder(7)]
        public int MVP { get; set; }

        [ColumnOrder(8)]
        public string HOF { get; set; }

        [ColumnOrder(9)]
        public string PC { get; set; }

        [ColumnOrder(10)]
        public string GOAT { get; set; }

        [ColumnOrder(11)]
        public string Collect { get; set; }

        [ColumnOrder(12)]
        public string CollectionArea { get; set; }
        #endregion
    }
}
