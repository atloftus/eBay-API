using eBay_API.Models.GoogleDrive;

namespace eBay_API.Utils
{
    public class BrandUtil
    {
        #region METHODS
        public static Brand? ToBrand(IList<object> row)
        {
            if (row == null || row.Count < 4) return null;

            return new Brand
            {
                Name = row[0]?.ToString(),
                Manufacturer = row[1]?.ToString(),
                Years = row[2]?.ToString(),
                Value = Int32.Parse(row[3]?.ToString())
            };
        }
        #endregion
    }
}