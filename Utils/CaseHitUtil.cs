using eBay_API.Models.GoogleDrive;

namespace eBay_API.Utils
{
    public class CaseHitUtil
    {
        #region METHODS
        public static CaseHit? ToCaseHit(IList<object> row)
        {
            if (row == null || row.Count < 4) return null;

            return new CaseHit
            {
                Name = row[0]?.ToString(),
                Set = row[1]?.ToString(),
                Image = row[2]?.ToString(),
                Type = row[3]?.ToString(),
                Value = Int32.Parse(row[4]?.ToString())
            };
        }
        #endregion
    }
}
