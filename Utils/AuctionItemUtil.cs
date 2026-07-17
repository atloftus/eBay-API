using eBay_API.Models.eBay.Response;
using eBay_API.Models.GoogleDrive;
using System.Text.RegularExpressions;

public static class AuctionItemUtil
{
    #region METHODS
    public static List<SportsAuctionItem> UnifyAndFilter(List<ItemSummary> newItems, List<SportsAuctionItem> oldItems, IEnumerable<string> filterWords, TimeZoneInfo centralZone)
    {
        var newAuctionItems = newItems
            .Select(item => SportsAuctionItem.FromItemSummary(item, centralZone))
            .ToList();

        return UnifyAndFilter(newAuctionItems, oldItems, filterWords, centralZone);
    }

    public static List<SportsAuctionItem> UnifyAndFilter(List<SportsAuctionItem> newItems, List<SportsAuctionItem> oldItems, IEnumerable<string> filterWords, TimeZoneInfo centralZone)
    {
        var allAuctionItems = oldItems
            .Concat(newItems)
            .ToList();

        allAuctionItems = allAuctionItems
            .Where(item => !string.IsNullOrWhiteSpace(item.Title) &&
                           !filterWords.Any(word => item.Title!.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0))
            .ToList();

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

        allAuctionItems = allAuctionItems
            .Where(item =>
            {
                if (string.IsNullOrWhiteSpace(item.Title)) return true;
                var titleLower = item.Title.ToLowerInvariant();
                bool isPanini = titleLower.Contains("panini");
                bool isUnlicensed = titleLower.Contains("unlicensed") ||
                                    titleLower.Contains("no license") ||
                                    titleLower.Contains("not licensed");

                if (!isPanini || !isUnlicensed)
                    return true;

                if (int.TryParse(item.Year, out int year))
                {
                    if (year >= 2025)
                        return false;
                }

                return true;
            })
            .ToList();

        allAuctionItems = allAuctionItems
            .GroupBy(item => item.ItemWebUrl, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderByDescending(i => int.TryParse(i.BidCount, out var bc) ? bc : 0).First())
            .ToList();

        allAuctionItems = allAuctionItems
            .GroupBy(item => item.Title?.Trim().ToLowerInvariant() ?? string.Empty)
            .Select(g => g.OrderByDescending(i => int.TryParse(i.BidCount, out var bc) ? bc : 0).First())
            .ToList();

        allAuctionItems = allAuctionItems
            .Where(item =>
            {
                DateTime endDate = DateTime.MinValue;
                if (!string.IsNullOrWhiteSpace(item.EndDate) && !string.IsNullOrWhiteSpace(item.EndTime))
                {
                    DateTime.TryParse($"{item.EndDate} {item.EndTime}", out endDate);
                    if (endDate != DateTime.MinValue)
                    {
                        endDate = TimeZoneInfo.ConvertTimeToUtc(endDate, centralZone);
                    }
                }
                return endDate > DateTime.UtcNow;
            })
            .ToList();

        allAuctionItems = allAuctionItems
            .Where(item => (int.TryParse(item.BidCount, out var bc) ? bc == 0 : true) && ((item.EndDateTime - item.StartDateTime) == TimeSpan.FromDays(5)))
            .ToList();

        return allAuctionItems;
    }

    public static List<PokemonAuctionItem> UnifyAndFilter(List<ItemSummary> newItems, List<PokemonAuctionItem> oldItems, IEnumerable<string> filterWords, TimeZoneInfo centralZone)
    {
        var newAuctionItems = newItems
            .Select(item => PokemonAuctionItem.FromItemSummary(item, centralZone))
            .ToList();

        return UnifyAndFilter(newAuctionItems, oldItems, filterWords, centralZone);
    }

    public static List<PokemonAuctionItem> UnifyAndFilter(List<PokemonAuctionItem> newItems, List<PokemonAuctionItem> oldItems, IEnumerable<string> filterWords, TimeZoneInfo centralZone)
    {
        var allAuctionItems = oldItems
            .Concat(newItems)
            .ToList();

        allAuctionItems = allAuctionItems
            .Where(item => !string.IsNullOrWhiteSpace(item.Title) &&
                           !filterWords.Any(word => item.Title!.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0))
            .ToList();

        allAuctionItems = allAuctionItems
            .GroupBy(item => item.ItemWebUrl, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderByDescending(i => int.TryParse(i.BidCount, out var bc) ? bc : 0).First())
            .ToList();

        allAuctionItems = allAuctionItems
            .GroupBy(item => item.Title?.Trim().ToLowerInvariant() ?? string.Empty)
            .Select(g => g.OrderByDescending(i => int.TryParse(i.BidCount, out var bc) ? bc : 0).First())
            .ToList();

        allAuctionItems = allAuctionItems
            .Where(item =>
            {
                DateTime endDate = DateTime.MinValue;
                if (!string.IsNullOrWhiteSpace(item.EndDate) && !string.IsNullOrWhiteSpace(item.EndTime))
                {
                    DateTime.TryParse($"{item.EndDate} {item.EndTime}", out endDate);
                    if (endDate != DateTime.MinValue)
                    {
                        endDate = TimeZoneInfo.ConvertTimeToUtc(endDate, centralZone);
                    }
                }
                return endDate > DateTime.UtcNow;
            })
            .ToList();

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

        var bracketMatches = Regex.Matches(title, @"[\[\(]([^()\[\]]{1,60})[\]\)]");
        foreach (Match m in bracketMatches)
        {
            var token = m.Groups[1].Value.Trim();
            if (Regex.IsMatch(token, @"^\d{4}$")) continue;
            if (Regex.IsMatch(token, @"^\d{1,4}/\d{1,4}$")) continue;
            if (token.Length >= 2) return token;
        }

        var setPattern = @"\b(Base Set|Jungle|Fossil|Team Rocket|Neo(?: )?Genesis|EX|XY|Sun & Moon|Sword & Shield|Scarlet & Violet|Shining Fates|Hidden Fates|Evolutions|Promos|Celebrations)\b";
        var setMatch = Regex.Match(title, setPattern, RegexOptions.IgnoreCase);
        if (setMatch.Success) return setMatch.Value;

        return "";
    }

    public static string ParseGeneration(string title)
    {
        if (string.IsNullOrWhiteSpace(title)) return "";

        var genMatch = Regex.Match(title, @"\bgen(?:eration)?\s*(\d{1,2})\b", RegexOptions.IgnoreCase);
        if (genMatch.Success && int.TryParse(genMatch.Groups[1].Value, out var explicitGen) && explicitGen >= 1 && explicitGen <= 10)
            return explicitGen.ToString();

        var yearStr = AuctionItemUtil.ParseCardYear(title);
        if (!int.TryParse(yearStr, out var yr))
            return "";

        var setToken = ParseSet(title)?.ToLowerInvariant() ?? "";
        bool setContains(string key) => !string.IsNullOrEmpty(setToken) && setToken.IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0;
        var tLower = title.ToLowerInvariant();

        switch (yr)
        {
            case int y when (y >= 1997 && y <= 2000):
                if (y == 2000)
                {
                    if (setContains("neo")) return "2";
                    return "1";
                }
                return "1";

            case int y when (y >= 2000 && y <= 2003):
                if (y == 2003)
                {
                    if (setContains("ex") || setContains("expedition") || setContains("aquapolis") || setContains("skyridge"))
                        return "3";
                    if (setContains("neo")) return "2";
                    return "2";
                }
                return "2";

            case int y when (y >= 2003 && y <= 2007):
                if (y == 2007)
                {
                    if (setContains("diamond") || setContains("pearl") || setContains("platinum") || setContains("heartgold") || setContains("soulsilver"))
                        return "4";
                    return "3";
                }
                return "3";

            case int y when (y >= 2007 && y <= 2011):
                if (y == 2011)
                {
                    if (setContains("black") || setContains("white") || tLower.Contains("black & white") || tLower.Contains("black/white") || setContains("bw"))
                        return "5";
                    return "4";
                }
                return "4";

            case int y when (y >= 2011 && y <= 2013):
                return "5";

            case int y when (y >= 2014 && y <= 2016):
                return "6";

            case int y when (y >= 2017 && y <= 2019):
                return "7";

            case int y when (y >= 2020 && y <= 2023):
                if (y == 2023)
                {
                    if (setContains("scarlet") || setContains("violet") || setContains("sv") || tLower.Contains("scarlet & violet"))
                        return "9";
                    return "8";
                }
                return "8";

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

            default:
                if (yr >= 2025) return "10";
                break;
        }

        return "";
    }

    public static string ParseCardNumber(string title)
    {
        if (string.IsNullOrWhiteSpace(title)) return "";

        var patterns = new[] {
                @"#?\s*\d{1,4}[A-Za-z]?\s*/\s*\d{1,4}\b",
                @"#\s*\d{1,4}[A-Za-z]?\b",
                @"\b[A-Z]{1,4}-\d{1,4}[A-Za-z]?\b",
                @"\b\d{1,4}[A-Za-z]?\b"
            };

        foreach (var p in patterns)
        {
            var m = Regex.Match(title, p, RegexOptions.IgnoreCase);
            if (!m.Success) continue;

            var val = m.Value.Trim();

            if (val.StartsWith("#")) val = val.Substring(1).Trim();

            var slashIndex = val.IndexOf('/');
            if (slashIndex >= 0)
            {
                val = val.Substring(0, slashIndex).Trim();
            }

            var hyphenIndex = val.IndexOf('-');
            if (hyphenIndex >= 0)
            {
                val = val.Substring(hyphenIndex + 1).Trim();
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



    public static void HydratePokemonAuctionItems(PokemonAuctionItem item, IEnumerable<string>? pokemonList, IEnumerable<string>? pokemonSetList)
    {
        var originalTitle = item.Title ?? string.Empty;
        var title = originalTitle;

        title.ToLower().Replace("gem mint", "").Replace("mint", "");

        // 1.) Parse year
        var yearVal = AuctionItemUtil.ParseCardYear(originalTitle);
        item.Year = yearVal ?? "";
        if (!string.IsNullOrEmpty(yearVal))
        {
            title = Regex.Replace(title, @"\b" + Regex.Escape(yearVal) + @"\b", " ", RegexOptions.IgnoreCase);
        }


        // 2.) Parse PSA/BGS/SGC/Beckett grade
        var gradePattern = @"\b(?:(PSA|BGS|SGC|CGC|PCG|TAG)\s*[:#\-\s]?\s*(\d{1,2}(?:\.\d)?)|(\d{1,2}(?:\.\d)?)\s*(?:/)?\s*(PSA|BGS|SGC|Beckett))\b";
        var gradeMatch = Regex.Match(title, gradePattern, RegexOptions.IgnoreCase);
        if (gradeMatch.Success)
        {
            if (gradeMatch.Groups[1].Success)
            {
                item.PSA = gradeMatch.Groups[2].Value;
            }
            else
            {
                item.PSA = gradeMatch.Groups[3].Value;
            }

            title = Regex.Replace(title, Regex.Escape(gradeMatch.Value), " ", RegexOptions.IgnoreCase);
        }


        // Use pokemonSetList (if provided) to attempt to parse the set name first, then fall back to ParseSet
        string parsedSet = "";
        if (pokemonSetList != null)
        {
            var normTitle = NormalizeToken(title);
            foreach (var setCandidate in pokemonSetList)
            {
                //TODO: Need to figure out how to handle case where its Team Rocket's XYZ vs Team Rocket set. Same thing with magama

                var normSet = NormalizeToken(setCandidate);
                if (normSet.Length < 2) continue;
                if (normTitle.Contains(normSet))
                {
                    parsedSet = setCandidate.Trim();
                    title = Regex.Replace(title, Regex.Escape(parsedSet), " ", RegexOptions.IgnoreCase);
                    break;
                }
            }


            if (string.IsNullOrEmpty(parsedSet))
            {
                if (normTitle.Contains("unlimited")) parsedSet = "base set";
                if (normTitle.Contains("shadowless")) parsedSet = "base set";
                if (normTitle.Contains("pokemon1stedition")) parsedSet = "base set";
                if (normTitle.Contains("pokemon2")) parsedSet = "base set 2";
                if (normTitle.Contains("pokemonpop")) parsedSet = "pop series 1";
                if (normTitle.Contains("expedition")) parsedSet = "expedition base set";
                if (normTitle.Contains("exfireredleafgreen")) parsedSet = "ex firered and leafgreen";
                if (normTitle.Contains("exteammagmavsaqua")) parsedSet = "ex team magma vs team aqua";
                if (normTitle.Contains("exrubysapphire")) parsedSet = "ex ruby and sapphire";
                if (normTitle.Contains("swshblackstarpromos")) parsedSet = "sword and shield promos";
                if (normTitle.Contains("diamondpearlblackstar")) parsedSet = "diamond and pearl promos";
                if (normTitle.Contains("mepblackstarpromos")) parsedSet = "mega evolution promos";
                if (normTitle.Contains("xypromos")) parsedSet = "x and y black star promos";
                if (normTitle.Contains("smblackstarpromos")) parsedSet = "sun and moon black star promos";
                if (normTitle.Contains("diamondpearl")) parsedSet = "diamond and pearl";
                if (normTitle.Contains("sunmoon")) parsedSet = "sun and moon";
                if (normTitle.Contains("blackwhite")) parsedSet = "black and white";
                if (normTitle.Contains("heartgoldandsoulsilver")) parsedSet = "heartgold and soulsilver";
                if (normTitle.Contains("hgss")) parsedSet = "heartgold and soulsilver";
                if (normTitle.Contains("sm")) parsedSet = "sun and moon";
                if (normTitle.Contains("xy")) parsedSet = "x and y";
                if (normTitle.Contains("sv")) parsedSet = "scarlet and violet";
                if (normTitle.Contains("swsh")) parsedSet = "sword and shield";
            }
        }

        item.Set = parsedSet ?? "";


        // Parse name and remove from title to avoid duplication
        var parsedName = AuctionItemUtil.ParseName(title, pokemonList);
        item.Name = parsedName ?? string.Empty;

        if (!string.IsNullOrWhiteSpace(parsedName) && !string.Equals(parsedName, "trainer", StringComparison.OrdinalIgnoreCase))
        {
            // remove bracketed name tokens like [Pikachu] or (Pikachu)
            title = Regex.Replace(title, @"[\[\(]\s*" + Regex.Escape(parsedName) + @"\s*[\]\)]", " ", RegexOptions.IgnoreCase);

            // remove any remaining exact literal occurrences of the name
            title = Regex.Replace(title, @"\b" + Regex.Escape(parsedName) + @"\b", " ", RegexOptions.IgnoreCase);
        }


        // Separate card number parsing from holo/variant parsing
        string cardNumber = "";
        string variantSuffix = "";

        // Improved patterns to capture alpha prefixes + numbers and optional "/..." parts:
        var cardPatterns = new[] {
                // #TG23/TG30, TG23/TG30, #RC10/RC25, RC10/RC25
                @"#?\s*[A-Za-z]{1,4}\d{1,4}[A-Za-z]?\s*/\s*[A-Za-z]{0,4}\d{1,4}[A-ZaZ]?\b",
                // #SWSH234, #DP27, #TG23
                @"#\s*[A-Za-z]{1,4}\d{1,4}[A-Za-z]?\b",
                // Set-123 or ABC-123 (keep behavior to extract numeric part after hyphen when present)
                @"\b[A-Za-z]{1,4}-\d{1,4}[A-Za-z]?\b",
                // fallback digit-only patterns
                @"\b\d{1,4}[A-Za-z]?\b"
            };
        Match? cardMatch = null;
        foreach (var p in cardPatterns)
        {
            var m = Regex.Match(title, p, RegexOptions.IgnoreCase);
            if (m.Success)
            {
                cardMatch = m;
                break;
            }
        }
        if (cardMatch != null)
        {
            var val = cardMatch.Value.Trim();

            // Strip leading '#' and any immediate whitespace
            val = Regex.Replace(val, @"^#\s*", "");

            // If there's a slash, prefer the left side (before '/')
            var slashIndex = val.IndexOf('/');
            if (slashIndex >= 0)
                val = val.Substring(0, slashIndex).Trim();

            // If token uses a hyphen like "SET-123", preserve existing behavior: take the part after the hyphen
            var hyphenIndex = val.IndexOf('-');
            if (hyphenIndex >= 0)
                val = val.Substring(hyphenIndex + 1).Trim();

            cardNumber = val.Trim();

            // remove matched card token from title
            title = Regex.Replace(title, Regex.Escape(cardMatch.Value), " ", RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(100));
        }

        item.CardNumber = cardNumber ?? "";


        // Holo detection - independent of card number logic
        var holoPattern = @"\breverse[\s-]?holo\b|\breverse holo\b|\bholo(graphic|g)?\b|\bfoil\b";
        var holo = AuctionItemUtil.ParseHoloType(title);
        if (string.IsNullOrEmpty(holo)) holo = "";
        item.HoloType = holo;

        if (Regex.IsMatch(title, holoPattern, RegexOptions.IgnoreCase))
        {
            title = Regex.Replace(title, holoPattern, " ", RegexOptions.IgnoreCase);
        }

        // Variant detection (V, VMAX, EX, GX, etc.) - treat as separate step and allow it to override holo type where appropriate
        var variantMatch = Regex.Match(title, @"\b(vmax|v-max|v|ex|e|gx|g)\b", RegexOptions.IgnoreCase);
        if (variantMatch.Success)
        {
            item.HoloType = variantMatch.Value.Trim().ToUpper();

            title = Regex.Replace(title, Regex.Escape(variantMatch.Value), " ", RegexOptions.IgnoreCase);
        }

        item.Generation = AuctionItemUtil.ParseGeneration(originalTitle);
    }
}