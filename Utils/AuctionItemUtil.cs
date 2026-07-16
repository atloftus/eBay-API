using eBay_API.Models.eBay.Response;
using eBay_API.Models.GoogleDrive;
using System.Text.RegularExpressions;


public static class AuctionItemUtil
{
    #region METHODS
    public static List<SportsAuctionItem> UnifyAndFilter(List<ItemSummary> newItems, List<SportsAuctionItem> oldItems, IEnumerable<string> filterWords, TimeZoneInfo centralZone)
    {
        // Convert new items to AuctionItem
        var newAuctionItems = newItems
            .Select(item => SportsAuctionItem.FromItemSummary(item, centralZone))
            .ToList();

        return UnifyAndFilter(newAuctionItems, oldItems, filterWords, centralZone);
    }


    public static List<SportsAuctionItem> UnifyAndFilter(List<SportsAuctionItem> newItems, List<SportsAuctionItem> oldItems, IEnumerable<string> filterWords, TimeZoneInfo centralZone)
    {
        // Combine new and old auction items
        var allAuctionItems = oldItems
            .Concat(newItems)
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


        // Remove Panini/Pannini unlicensed products from 2025 onward
        allAuctionItems = allAuctionItems
            .Where(item =>
            {
                if (string.IsNullOrWhiteSpace(item.Title)) return true;
                var titleLower = item.Title.ToLowerInvariant();

                // match common spellings of Panini
                bool isPanini = titleLower.Contains("panini");

                // match obvious "unlicensed" indicators - extendable if needed
                bool isUnlicensed = titleLower.Contains("unlicensed") ||
                                    titleLower.Contains("no license") ||
                                    titleLower.Contains("not licensed");

                // Only exclude when both panini and unlicensed indicators are present and year >= 2025
                if (!isPanini || !isUnlicensed)
                    return true;

                if (int.TryParse(item.Year, out int year))
                {
                    if (year >= 2025)
                        return false; // filter out
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
            .Where(item => (int.TryParse(item.BidCount, out var bc) ? bc == 0 : true) && ((item.EndDateTime - item.StartDateTime) == TimeSpan.FromDays(5)))
            .ToList();

        return allAuctionItems;
    }


    public static List<PokemonAuctionItem> UnifyAndFilter(List<ItemSummary> newItems, List<PokemonAuctionItem> oldItems, IEnumerable<string> filterWords, TimeZoneInfo centralZone)
    {
        // Convert new items to AuctionItem
        var newAuctionItems = newItems
            .Select(item => PokemonAuctionItem.FromItemSummary(item, centralZone))
            .ToList();

        return UnifyAndFilter(newAuctionItems, oldItems, filterWords, centralZone);
    }


    public static List<PokemonAuctionItem> UnifyAndFilter(List<PokemonAuctionItem> newItems, List<PokemonAuctionItem> oldItems, IEnumerable<string> filterWords, TimeZoneInfo centralZone)
    {
        // Combine new and old auction items
        var allAuctionItems = oldItems
            .Concat(newItems)
            .ToList();

        // Filter by title words (case-insensitive)
        allAuctionItems = allAuctionItems
            .Where(item => !string.IsNullOrWhiteSpace(item.Title) &&
                           !filterWords.Any(word => item.Title!.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0))
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

        //// Remove all items that have a bid count higher than 0
        //allAuctionItems = allAuctionItems
        //    .Where(item => (int.TryParse(item.BidCount, out var bc) ? bc == 0 : true) && ((item.EndDateTime - item.StartDateTime) == TimeSpan.FromDays(5)))
        //    .ToList();

        return allAuctionItems;
    }


    public static SportsAuctionItem? ToAuctionItem(IList<object> row)
    {
        if (row == null || row.Count < 10) return null;

        DateTime itemEndDate = DateTime.MinValue;
        if (DateTime.TryParse($"{row[4]?.ToString()} {row[5]?.ToString()}", out var dt))
            itemEndDate = dt;

        return new SportsAuctionItem
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


    public static string ParseCardYear(string title)
    {
        if (string.IsNullOrWhiteSpace(title)) return "";
        var yearMatch = Regex.Match(title, @"\b(\d{4})(?:-(\d{2}))?\b");
        if (yearMatch.Success) return yearMatch.Groups[1].Value;
        return "";
    }


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


    public static string ParseAuto(string title)
    {
        if (string.IsNullOrWhiteSpace(title)) return "No";
        var lower = title.ToLowerInvariant();
        return (lower.Contains(" auto ") || lower.Contains(" autograph ") || lower.Contains(" signatures ") || lower.Contains(" signings ")) ? "Yes" : "No";
    }


    public static string ParsePatch(string title)
    {
        if (string.IsNullOrWhiteSpace(title)) return "No";
        var lower = title.ToLowerInvariant();
        return (lower.Contains(" jersey ") || lower.Contains(" patch ") || lower.Contains(" materials ")) ? "Yes" : "No";
    }


    public static string ParseCaseHit(string title)
    {
        if (string.IsNullOrWhiteSpace(title)) return "No";
        var lower = title.ToLowerInvariant();
        return (lower.Contains(" case hit ") || lower.Contains(" ssp ") || lower.Contains(" sp ")) ? "Yes" : "No";
    }


    public static string FormatUrl(string url)
    {
        if (string.IsNullOrEmpty(url)) return "";
        if (url.Contains(",") || url.Contains("\""))
            return $"\"{url.Replace("\"", "\"\"")}\"";
        return url;
    }


    public static string NormalizeToken(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "";
        var arr = s
            .Where(c => char.IsLetterOrDigit(c))
            .Select(c => char.ToLowerInvariant(c))
            .ToArray();
        return new string(arr);
    }


    public static string? ParseName(string title, IEnumerable<string>? pokemonList)
    {
        if (string.IsNullOrWhiteSpace(title)) return null;
        if (pokemonList == null) return null;

        var normTitle = NormalizeToken(title);

        foreach (var p in pokemonList)
        {
            if (p == null) continue;
            var normName = NormalizeToken(p);
            if (string.IsNullOrEmpty(normName)) continue;
            if (normTitle.Contains(normName)) return p;
        }

        return "trainer";
    }


    public static string ParseSet(string title)
    {
        if (string.IsNullOrWhiteSpace(title)) return "";

        // prefer bracketed or parenthesized tokens (common in listings)
        var bracketMatches = Regex.Matches(title, @"[\[\(]([^()\[\]]{1,60})[\]\)]");
        foreach (Match m in bracketMatches)
        {
            var token = m.Groups[1].Value.Trim();
            // ignore tokens that are just years or card numbers
            if (Regex.IsMatch(token, @"^\d{4}$")) continue;
            if (Regex.IsMatch(token, @"^\d{1,4}/\d{1,4}$")) continue;
            if (token.Length >= 2) return token;
        }

        // fallback: try to match common set words sequences (conservative)
        var setPattern = @"\b(Base Set|Jungle|Fossil|Team Rocket|Neo(?: )?Genesis|EX|XY|Sun & Moon|Sword & Shield|Scarlet & Violet|Shining Fates|Hidden Fates|Evolutions|Promos|Celebrations)\b";
        var setMatch = Regex.Match(title, setPattern, RegexOptions.IgnoreCase);
        if (setMatch.Success) return setMatch.Value;

        return "";
    }


    public static string ParseGeneration(string title)
    {
        if (string.IsNullOrWhiteSpace(title)) return "";

        // 1) explicit "Gen" or "Generation" mention
        var genMatch = Regex.Match(title, @"\bgen(?:eration)?\s*(\d{1,2})\b", RegexOptions.IgnoreCase);
        if (genMatch.Success && int.TryParse(genMatch.Groups[1].Value, out var explicitGen) && explicitGen >= 1 && explicitGen <= 10)
            return explicitGen.ToString();

        // 2) year-based switch (primary logic). If the year sits on an overlap boundary,
        //    use set token to disambiguate.
        var yearStr = AuctionItemUtil.ParseCardYear(title);
        if (!int.TryParse(yearStr, out var yr))
            return "";

        var setToken = ParseSet(title)?.ToLowerInvariant() ?? "";
        bool setContains(string key) => !string.IsNullOrEmpty(setToken) && setToken.IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0;
        var tLower = title.ToLowerInvariant();

        switch (yr)
        {
            // Generation 1: 1999–2000 (2000 overlaps with Gen 2 -> check set)
            case int y when (y >= 1999 && y <= 2000):
                if (y == 2000)
                {
                    if (setContains("neo")) return "2";
                    return "1";
                }
                return "1";

            // Generation 2: 2000–2003 (2003 overlaps with Gen 3 -> check set)
            case int y when (y >= 2000 && y <= 2003):
                if (y == 2003)
                {
                    if (setContains("ex") || setContains("expedition") || setContains("aquapolis") || setContains("skyridge"))
                        return "3";
                    if (setContains("neo")) return "2";
                    return "2";
                }
                return "2";

            // Generation 3: 2003–2007 (2007 overlaps with Gen 4 -> check set)
            case int y when (y >= 2003 && y <= 2007):
                if (y == 2007)
                {
                    if (setContains("diamond") || setContains("pearl") || setContains("platinum") || setContains("heartgold") || setContains("soulsilver"))
                        return "4";
                    return "3";
                }
                return "3";

            // Generation 4: 2007–2011 (2011 overlaps with Gen 5 -> check set)
            case int y when (y >= 2007 && y <= 2011):
                if (y == 2011)
                {
                    if (setContains("black") || setContains("white") || tLower.Contains("black & white") || tLower.Contains("black/white") || setContains("bw"))
                        return "5";
                    return "4";
                }
                return "4";

            // Generation 5: 2011–2013
            case int y when (y >= 2011 && y <= 2013):
                return "5";

            // Generation 6: 2014–2016
            case int y when (y >= 2014 && y <= 2016):
                return "6";

            // Generation 7: 2017–2019
            case int y when (y >= 2017 && y <= 2019):
                return "7";

            // Generation 8: 2020–2023 (2023 overlaps with Gen 9 -> check set)
            case int y when (y >= 2020 && y <= 2023):
                if (y == 2023)
                {
                    if (setContains("scarlet") || setContains("violet") || setContains("sv") || tLower.Contains("scarlet & violet"))
                        return "9";
                    return "8";
                }
                return "8";

            // Generation 9: 2023–2025 (2025 overlaps with Gen 10 -> check set)
            case int y when (y >= 2023 && y <= 2025):
                if (y == 2023)
                {
                    if (setContains("scarlet") || setContains("violet") || setContains("sv") || tLower.Contains("scarlet & violet"))
                        return "9";
                    return "8";
                }
                if (y == 2025)
                {
                    if (setContains("mega") || tLower.Contains("mega evolution")) return "10";
                    return "9";
                }
                return "9";

            // Generation 10: 2025–present
            default:
                if (yr >= 2025) return "10";
                break;
        }

        return "";
    }


    public static string ParseCardNumber(string title)
    {
        if (string.IsNullOrWhiteSpace(title)) return "";

        //TODO: be able to parse #50a /147 or #50a/147 as "50a"
        var patterns = new[]    {
                @"\b\d{1,4}/\d{1,4}\b",
                @"#\d{1,4}\b",
                @"\b[A-Z]{1,3}-\d{1,4}\b",
                @"\b\d{1,4}\b"
            };

        foreach (var p in patterns)
        {
            var m = Regex.Match(title, p, RegexOptions.IgnoreCase);
            if (!m.Success) continue;

            var val = m.Value.Trim();

            if (val.StartsWith("#")) val = val.Substring(1);

            var slashIndex = val.IndexOf('/');
            if (slashIndex >= 0)
            {
                val = val.Substring(0, slashIndex);
            }

            return val.Trim();
        }

        return "";
    }


    public static string ParseHoloType(string title)
    {
        if (string.IsNullOrWhiteSpace(title)) return "Non-Holo";

        if (Regex.IsMatch(title, @"reverse[\s-]?holo", RegexOptions.IgnoreCase)) return "Reverse Holo";
        if (Regex.IsMatch(title, @"\breverse holo\b", RegexOptions.IgnoreCase)) return "Reverse Holo";
        if (Regex.IsMatch(title, @"\bholo(graphic|g)?\b", RegexOptions.IgnoreCase)) return "Holo";
        if (Regex.IsMatch(title, @"\bfoil\b", RegexOptions.IgnoreCase)) return "Holo";

        return "";
    }
    #endregion








    //NNEW CODE
    public static void HydratePokemonAuctionItems(PokemonAuctionItem item)
    {

    }
}