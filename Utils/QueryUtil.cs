namespace eBay_API.Utils
{
    public class QueryUtil
    {
        #region METHODS
        public static string InjectSeller(string query, string seller)
        {
            int filterIndex = query.IndexOf("filter=");
            if (filterIndex >= 0)
            {
                int insertIndex = query.IndexOf(',', filterIndex);
                if (insertIndex >= 0)
                {
                    return query.Insert(insertIndex, $",sellers:{{{seller}}}");
                }
                else
                {
                    return query + $",sellers:{{{seller}}}";
                }
            }
            else
            {
                return query + $",sellers:{{{seller}}}";
            }
        }
        #endregion
    }
}
