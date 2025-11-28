using eBay_API.Models.GoogleDrive;

namespace eBay_API.Utils
{
    public class PlayerUtil
    {
        #region METHODS
        public static Player? ToPlayer(IList<object> row)
        {
            if (row == null || row.Count < 6) return null;

            return new Player
            {
                Name = row[0]?.ToString() ?? string.Empty,
                Position = row[1]?.ToString() ?? string.Empty,  
                CollectionRCYear = int.TryParse(row[2]?.ToString(), out var rcYear) ? rcYear : 0,
                StartYear = int.TryParse(row[3]?.ToString(), out var startYear) ? startYear : 0,
                EndYear = int.TryParse(row[4]?.ToString(), out var endYear) ? endYear : 0,
                Status = row[5]?.ToString() ?? string.Empty,
                MVP = int.TryParse(row[6]?.ToString() ?? "0", out var mvp) ? mvp : 0,
                HOF = row[7]?.ToString() ?? string.Empty,
                PC = row[8]?.ToString() ?? string.Empty,
                GOAT = row[9]?.ToString() ?? string.Empty,
                Collect = row[10]?.ToString() ?? string.Empty,
                CollectionArea = row[11]?.ToString() ?? string.Empty
            };
        }
        #endregion
    }
}