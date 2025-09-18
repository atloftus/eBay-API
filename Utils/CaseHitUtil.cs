using eBay_API.Models.GoogleDrive;

namespace eBay_API.Utils
{
    public class CaseHitUtil
    {
        #region METHODS
        public static CaseHit? ToCaseHit(IList<object> row)
        {
            if (row == null || row.Count < 6) return null;

            return new CaseHit
            {
                Sport = row[0]?.ToString(),
                Name = row[1]?.ToString(),
                Set = row[2]?.ToString(),
                Years = row[3]?.ToString() ?? "0",
                Type = row[4]?.ToString(),
                Value = Int32.Parse(row[5]?.ToString())
            };
        }

        #endregion
    }
}
