namespace eBay_API.Utils
{
    public static class RequestUtil
    {
        #region PROPERTIES
        // No properties in this utility class.
        #endregion



        #region METHODS
        /// <summary>
        /// Parses a string value into a SaveTarget enum.
        /// </summary>
        /// <param name="s">Input string to parse.</param>
        /// <returns>Corresponding SaveTarget value, or Unknown if not matched.</returns>
        public static SaveTarget ParseSaveTarget(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return SaveTarget.Unknown;
            s = s.Trim().ToLowerInvariant();
            return s switch
            {
                "1" or "csv" or "localcsv" => SaveTarget.Csv,
                "2" or "sheets" or "sheet" or "google" or "googlesheet" => SaveTarget.Sheets,
                _ => SaveTarget.Unknown
            };
        }
        #endregion



        #region INTERNALS
        /// <summary>
        /// Indicates the target for saving data.
        /// </summary>
        public enum SaveTarget { Unknown = 0, Csv = 1, Sheets = 2 }
        #endregion
    }
}