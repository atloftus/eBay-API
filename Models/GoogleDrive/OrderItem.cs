using System.Globalization;
using eBay_API.Models.eBay.Response;



namespace eBay_API.Models.GoogleDrive
{
    public class OrderItem : TableRow
    {
        #region PROPERTIES
        [ColumnOrder(1)]
        public string Created { get; set; }

        [ColumnOrder(2)]
        public string Title { get; set; }

        [ColumnOrder(3)]
        public string Rookie
        {
            get
            {
                return AuctionItemUtil.ParseRC(Title).ToString();
            }
        }

        [ColumnOrder(4)]
        public string OutOf
        {
            get
            {
                return AuctionItemUtil.ParseOutOf(Title).ToString();
            }
        }

        [ColumnOrder(5)]
        public string PSA
        {
            get
            {
                return AuctionItemUtil.ParsePSA(Title).ToString();
            }
        }

        [ColumnOrder(6)]
        public string Price { get; set; }

        [ColumnOrder(7)]
        public string TaxAmount { get; set; }

        [ColumnOrder(8)]
        public string ShippingAmount { get; set; }

        [ColumnOrder(9)]
        public string TotalAmount
        {
            get
            {
                decimal price = ParseDollarAmount(Price);
                decimal tax = ParseDollarAmount(TaxAmount);
                decimal shipping = ParseDollarAmount(ShippingAmount);
                return $"${(price + tax + shipping):0.00}";
            }
        }

        [ColumnOrder(10)]
        public string ItemId { get; set; }

        [ColumnOrder(11)]
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


        private static decimal ParseDollarAmount(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return 0m;

            // Remove dollar sign and any whitespace
            var cleaned = value.Replace("$", "").Trim();

            return decimal.TryParse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture, out var result)
                ? result
                : 0m;
        }
        #endregion
    }
}