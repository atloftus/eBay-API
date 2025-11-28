namespace eBay_API.Models.GoogleDrive
{
    public class Brand
    {
        #region PROPERTIES
        [ColumnOrder(1)]
        public string Name { get; set; }

        [ColumnOrder(2)]
        public string Manufacturer { get; set; }

        [ColumnOrder(3)]
        public string Years { get; set; }

        [ColumnOrder(4)]
        public int Value { get; set; }
        #endregion
    }
}
