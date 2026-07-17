using System.Globalization;
using eBay_API.Models.GoogleDrive;

namespace eBay_API.Utils
{
    public static class PokemonCardUtil
    {
        #region METHODS
        public static PokemonCard? ToPokemonCard(IList<object> row)
        {
            if (row == null || row.Count == 0) return null;

            static string GetCell(IList<object> r, int index) => index < r.Count ? (r[index]?.ToString() ?? string.Empty) : string.Empty;

            string setNumber = GetCell(row, 0).Trim().Split("/")[0];
            string name = GetCell(row, 1).Trim().ToLower();
            string type = GetCell(row, 2).Trim();

            static int ParseIntCell(string s)
            {
                if (string.IsNullOrWhiteSpace(s)) return 0;
                s = s.Trim();
                if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i)) return i;
                if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var d)) return (int)d;
                return 0;
            }

            int qualityNormal = ParseIntCell(GetCell(row, 3));
            int qualityReverse = ParseIntCell(GetCell(row, 4));
            int qualityHolo = ParseIntCell(GetCell(row, 5));

            return new PokemonCard
            {
                CardNumber = setNumber,
                Name = name,
                Type = type,
                QualityNormal = qualityNormal,
                QualityReverse = qualityReverse,
                QualityHolo = qualityHolo
            };
        }
        #endregion
    }
}