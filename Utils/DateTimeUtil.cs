namespace eBay_API.Utils
{
    public static class DateTimeUtil
    {
        #region PROPERTIES
        // No properties in this static utility class.
        #endregion


        #region CONSTRUCTORS
        // No constructors in this static utility class.
        #endregion


        #region METHODS
        /// <summary>
        /// Finds the Central Time Zone for use in date normalization.
        /// Attempts multiple system IDs for compatibility across Windows and Linux.
        /// </summary>
        /// <returns>
        /// The TimeZoneInfo for Central Standard Time (Windows) or America/Chicago (Linux).
        /// </returns>
        public static TimeZoneInfo FindCentralTimeZone()
        {
            // Try Windows ID first, fallback to Linux ID if not found or invalid.
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time");
            }
            catch (TimeZoneNotFoundException)
            {
                return TimeZoneInfo.FindSystemTimeZoneById("America/Chicago");
            }
            catch (InvalidTimeZoneException)
            {
                return TimeZoneInfo.FindSystemTimeZoneById("America/Chicago");
            }
        }
        #endregion
    }
}