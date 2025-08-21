using System.Globalization;
using eBay_API.Models.eBay.Response;

namespace eBay_API.Models.GoogleDrive
{
    public class OrderItem : TableRow
    {
        #region PROPERTIES

        [ColumnOrder(1)]
        public string Title { get; set; }
        [ColumnOrder(2)]
        public string Price { get; set; }
        [ColumnOrder(3)]
        public string TaxAmount { get; set; }
        [ColumnOrder(4)]
        public string ShippingAmount { get; set; }

        // Property that adds up price, tax, and shipping
        [ColumnOrder(5)]
        public string TotalAmount
        {
            get
            {
                decimal price = decimal.TryParse(Price, NumberStyles.Any, CultureInfo.InvariantCulture, out var p) ? p : 0m;
                decimal tax = decimal.TryParse(TaxAmount, NumberStyles.Any, CultureInfo.InvariantCulture, out var t) ? t : 0m;
                decimal shipping = decimal.TryParse(ShippingAmount, NumberStyles.Any, CultureInfo.InvariantCulture, out var s) ? s : 0m;
                return (price + tax + shipping).ToString("0.00", CultureInfo.InvariantCulture);
            }
        }
        [ColumnOrder(6)]
        public string Created { get; set; }
        public string ItemId { get; set; }

        [ColumnOrder(7)]
        public string ItemLink
        {
            get
            {
                return string.IsNullOrWhiteSpace(ItemId)
                    ? ""
                    : $"https://www.ebay.com/itm/{ItemId}";
            }
        }

        #endregion

        #region METHODS
        /// <summary>
        /// Converts a LineItem to an OrderItem, normalizing and formatting fields for Google Sheets.
        /// </summary>
        /// <param name="lineItem">The LineItem to convert.</param>
        /// <returns>A new OrderItem instance.</returns>
        public static OrderItem FromLineItem(LineItem lineItem)
        {
            // Format Created date as yyyy-MM-dd HH:mm:ss for Google Sheets
            string createdStr = lineItem.Created.HasValue
                ? lineItem.Created.Value.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
                : "";

            return new OrderItem
            {
                ItemId = lineItem.ItemId ?? "",
                Title = lineItem.Title ?? "",
                Price = lineItem.Price.HasValue
                    ? lineItem.Price.Value.ToString("0.00", CultureInfo.InvariantCulture)
                    : "",
                Created = createdStr,
                TaxAmount = lineItem.TaxAmount.HasValue
                    ? lineItem.TaxAmount.Value.ToString("0.00", CultureInfo.InvariantCulture)
                    : "",
                ShippingAmount = lineItem.ShippingAmount.HasValue
                    ? lineItem.ShippingAmount.Value.ToString("0.00", CultureInfo.InvariantCulture)
                    : ""
            };
        }
        #endregion
    }
}