using eBay_API.Models.eBay.Response;
using eBay_API.Models.GoogleDrive;
using System.Text.RegularExpressions;


public static class AuctionItemUtil
{
    #region METHODS
    /// <summary>
    /// Unifies new and old auction items, applies multiple filters, removes duplicates and expired items.
    /// </summary>
    /// <param name="newItems">List of new ItemSummary objects.</param>
    /// <param name="oldItems">List of old AuctionItem objects.</param>
    /// <param name="filterWords">Words to filter out from item titles.</param>
    /// <param name="centralZone">Time zone for date normalization.</param>
    /// <returns>Filtered and unified list of AuctionItem objects.</returns>
    public static List<AuctionItem> UnifyAndFilter(List<ItemSummary> newItems, List<AuctionItem> oldItems, IEnumerable<string> filterWords, TimeZoneInfo centralZone)
    {
        // Convert new items to AuctionItem
        var newAuctionItems = newItems
            .Select(item => AuctionItem.FromItemSummary(item, centralZone))
            .ToList();

        // Combine new and old auction items
        var allAuctionItems = oldItems
            .Concat(newAuctionItems)
            .ToList();

        // Filter by title words (case-insensitive)
        allAuctionItems = allAuctionItems
            .Where(item => !string.IsNullOrWhiteSpace(item.Title) &&
                           !filterWords.Any(word => item.Title!.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0))
            .ToList();

        // Filter out Topps or Finest from 2016-2026
        allAuctionItems = allAuctionItems 
            .Where(item =>
            {
                if (item.Title == null) return true;
                var titleLower = item.Title.ToLowerInvariant();
                bool containsToppsOrFinest = titleLower.Contains("topps") || titleLower.Contains("finest");
                if (!containsToppsOrFinest)
                    return true;
                var yearStr = item.Year;
                if (int.TryParse(yearStr, out int year))
                {
                    if (year >= 2016 && year <= 2026)
                        return false;
                }
                return true;
            })
            .ToList();

        // Filter out Bowman cards before 2020
        allAuctionItems = allAuctionItems
            .Where(item =>
            {
                if (item.Title == null) return true;
                var titleLower = item.Title.ToLowerInvariant();
                bool containsBowman = titleLower.Contains("bowman");
                if (!containsBowman)
                    return true;
                var yearStr = item.Year;
                if (int.TryParse(yearStr, out int year))
                {
                    if (year < 2020)
                        return false;
                }
                return true;
            })
            .ToList();

        // Remove duplicates by ItemWebUrl, keep item with higher BidCount
        allAuctionItems = allAuctionItems
            .GroupBy(item => item.ItemWebUrl, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderByDescending(i => int.TryParse(i.BidCount, out var bc) ? bc : 0).First())
            .ToList();

        // Remove duplicates by Title, keep item with higher BidCount
        allAuctionItems = allAuctionItems
            .GroupBy(item => item.Title?.Trim().ToLowerInvariant() ?? string.Empty)
            .Select(g => g.OrderByDescending(i => int.TryParse(i.BidCount, out var bc) ? bc : 0).First())
            .ToList();


        // Remove expired items
        allAuctionItems = allAuctionItems
            .Where(item =>
            {
                DateTime endDate = DateTime.MinValue;
                if (!string.IsNullOrWhiteSpace(item.EndDate) && !string.IsNullOrWhiteSpace(item.EndTime))
                {
                    DateTime.TryParse($"{item.EndDate} {item.EndTime}", out endDate);
                    // Assume endDate is in centralZone, convert to UTC
                    if (endDate != DateTime.MinValue)
                    {
                        endDate = TimeZoneInfo.ConvertTimeToUtc(endDate, centralZone);
                    }
                }
                return endDate > DateTime.UtcNow;
            })
            .ToList();


        // Remove all items that have a bid count higher than 0
        allAuctionItems = allAuctionItems
            .Where(item => int.TryParse(item.BidCount, out var bc) ? bc == 0 : true)
            .ToList();

        return allAuctionItems;
    }


    /// <summary>
    /// Converts a Google Sheet row to an AuctionItem object.
    /// </summary>
    /// <param name="row">Row from Google Sheet.</param>
    /// <returns>AuctionItem object or null if row is invalid.</returns>
    public static AuctionItem? ToAuctionItem(IList<object> row)
    {
        if (row == null || row.Count < 10) return null;

        DateTime itemEndDate = DateTime.MinValue;
        if (DateTime.TryParse($"{row[4]?.ToString()} {row[5]?.ToString()}", out var dt))
            itemEndDate = dt;

        return new AuctionItem
        {
            Title = row[0]?.ToString(),
            Year = row[1]?.ToString(),
            Price = row[2]?.ToString(),
            BidCount = row[3]?.ToString() ?? "0",
            EndDate = row[4]?.ToString(),
            EndTime = row[5]?.ToString(),
            OutOf = row[7]?.ToString(),
            Rookie = row[8]?.ToString(),
            ItemWebUrl = row[9]?.ToString()
        };
    }


    public static string ParseTitle(string title) =>
    title?.Replace("\"", "\"\"") ?? "";


    public static int ParseOutOf(string title)
    {
        if (string.IsNullOrEmpty(title)) return 999999;
        var match = Regex.Match(title, @"(?:#\d+/(\d{1,5}))|(?:/(\d{1,5})(?:\s|$))");
        if (match.Success)
        {
            string value = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
            if (int.TryParse(value, out int parsed))
                return parsed;
        }
        else if (Regex.Match(title, @"#\d+").Success)
        {
            return 999999;
        }
        return 999999;
    }


    public static string ParsePSA(string title)
    {
        if (string.IsNullOrEmpty(title)) return "0";
        var psaMatch = Regex.Match(title, @"PSA (\d{1,2})");
        return psaMatch.Success ? psaMatch.Groups[1].Value : "0";
    }


    public static string ParseRC(string title)
    {
        if (string.IsNullOrWhiteSpace(title)) return "No";
        var lower = title.ToLowerInvariant();
        return (lower.Contains(" rc ") || lower.Contains(" rookie ")) ? "Yes" : "No";
    }


    public static string ParseCaseHits(string title)
    {
        //TODO: Implement this logic by checking to see if the title contains any of the case hit tittles in the list
        return "No";
    }


    public static string FormatUrl(string url)
    {
        if (string.IsNullOrEmpty(url)) return "";
        if (url.Contains(",") || url.Contains("\""))
            return $"\"{url.Replace("\"", "\"\"")}\"";
        return url;
    }
    #endregion
}