namespace eBay_API.Models.GoogleDrive
{
    public class CaseHit : TableRow
    {
        #region PROPERTIES
        [ColumnOrder(1)]
        public string Sport { get; set; }

        [ColumnOrder(2)]
        public string Name { get; set; }

        [ColumnOrder(3)]
        public string Set { get; set; }

        [ColumnOrder(4)]
        public string Years { get; set; }

        [ColumnOrder(5)]
        public string Type { get; set; }

        [ColumnOrder(6)]
        public int Number { get; set; }

        [ColumnOrder(7)]
        public int Value { get; set; }
        #endregion
    }
}