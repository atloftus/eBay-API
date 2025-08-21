using System.Globalization;
using System.Text.RegularExpressions;
using eBay_API.Models.Config;
using eBay_API.Models.eBay.Response;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;



public class GoogleDriveService
{
    #region PROPERTIES
    private readonly GoogleDriveConfig _config;
    private Google.Apis.Sheets.v4.SheetsService _sheetService;
    public sealed record RowUpdate(int RowNumber, IList<object> Values);
    #endregion



    #region CONSTRUCTORS
    /// <summary>Initializes a new instance of the <see cref="GoogleDriveService"/> class.</summary>
    /// <param name="config">Google Drive configuration.</param>
    public GoogleDriveService(GoogleDriveConfig config)
    {
        _config = config;
        GoogleCredential credential;
        using (var stream = new FileStream("googledrivecred.json", FileMode.Open, FileAccess.Read))
        {
            credential = GoogleCredential.FromStream(stream).CreateScoped(new string[] { SheetsService.Scope.Spreadsheets });
        }
        _sheetService = new SheetsService(new BaseClientService.Initializer()
        {
            HttpClientInitializer = credential,
            ApplicationName = _config.applicationname,
        });
    }
    #endregion


   
    #region METHODS
    /// <summary>
    /// Gets all rows from the specified sheet using a row factory.
    /// </summary>
    /// <typeparam name="T">Type to convert each row to.</typeparam>
    /// <param name="sheetName">Sheet name.</param>
    /// <param name="rowFactory">Function to convert a row.</param>
    /// <returns>List of rows converted to type T.</returns>
    public async Task<List<T>> GetAllRowsAsync<T>(string sheetName, Func<IList<object>, T> rowFactory)
    {
        if (string.IsNullOrWhiteSpace(sheetName)) throw new ArgumentException("Sheet name is required", nameof(sheetName));
        var spreadsheetId = _config.sheetid;
        var existingId = await GetSheetIdAsync(sheetName).ConfigureAwait(false);
        if (!existingId.HasValue) return new List<T>();
        var getRequest = _sheetService.Spreadsheets.Values.Get(spreadsheetId, $"{sheetName}!A2:Z");
        var getResponse = await getRequest.ExecuteAsync().ConfigureAwait(false);
        var rows = getResponse.Values?.ToList() ?? new List<IList<object>>();
        var result = new List<T>();
        if (rowFactory != null)
        {
            foreach (var row in rows)
            {
                result.Add(rowFactory(row));
            }
        }
        return result;
    }


    /// <summary>
    /// Writes all items to the specified sheet, including the header row.
    /// </summary>
    /// <typeparam name="T">Type of item.</typeparam>
    /// <param name="allItems">All items to write.</param>
    /// <param name="sheetName">Sheet name.</param>
    /// <param name="rowSelector">Function to select row values.</param>
    /// <param name="headerRow">Header row values.</param>
    public async Task WriteItemsAsync<T>(List<T> allItems, string sheetName, Func<T, IList<object>> rowSelector, IList<string> headerRow)
    {
        string spreadsheetId = _config.sheetid;
        var values = new List<IList<object>> { headerRow.ToList<object>() };
        foreach (var item in allItems)
        {
            values.Add(rowSelector(item));
        }
        var updateRequest = _sheetService.Spreadsheets.Values.Update(new ValueRange { Values = values }, spreadsheetId, $"{sheetName}!A1");
        updateRequest.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.USERENTERED;
        await updateRequest.ExecuteAsync();
        Console.WriteLine($"Combined data written to Google Sheet: {sheetName}");
    }


    /// <summary>
    /// Creates a new tab in the Google Sheet and writes headers if provided.
    /// </summary>
    /// <param name="sheetName">Sheet name.</param>
    /// <param name="headers">Optional header row.</param>
    /// <returns>True if tab created, false if already exists.</returns>
    public async Task<bool> CreateTabAsync(string sheetName, IList<string>? headers = null)
    {
        if (string.IsNullOrWhiteSpace(sheetName)) throw new ArgumentException("Sheet name is required", nameof(sheetName));
        var spreadsheetId = _config.sheetid;
        var existingId = await GetSheetIdAsync(sheetName).ConfigureAwait(false);
        if (existingId.HasValue) return false;
        var addReq = new Request
        {
            AddSheet = new AddSheetRequest
            {
                Properties = new SheetProperties { Title = sheetName }
            }
        };
        var batch = new BatchUpdateSpreadsheetRequest { Requests = new List<Request> { addReq } };
        var op = _sheetService.Spreadsheets.BatchUpdate(batch, spreadsheetId);
        await op.ExecuteAsync().ConfigureAwait(false);
        if (headers is { Count: > 0 })
        {
            var vr = new ValueRange
            {
                Values = new List<IList<object>> { headers.Cast<object>().ToList() }
            };
            var upd = _sheetService.Spreadsheets.Values.Update(vr, spreadsheetId, $"{sheetName}!A1");
            upd.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.USERENTERED;
            await upd.ExecuteAsync().ConfigureAwait(false);
        }
        return true;
    }


    /// <summary>
    /// Appends item rows to the specified sheet, optionally deduplicating by URL.
    /// </summary>
    /// <param name="items">Items to append.</param>
    /// <param name="sheetName">Sheet name.</param>
    /// <param name="centralZone">Time zone for date conversion.</param>
    /// <param name="dedupeByUrl">Whether to deduplicate by URL.</param>
    /// <returns>Number of rows appended.</returns>
    public async Task<int> AppendItemsAsync(List<ItemSummary> items, string sheetName, TimeZoneInfo centralZone, bool dedupeByUrl = false)
    {
        if (items is null) throw new ArgumentNullException(nameof(items));
        if (string.IsNullOrWhiteSpace(sheetName)) throw new ArgumentException("Sheet name is required", nameof(sheetName));
        var spreadsheetId = _config.sheetid;
        IEnumerable<ItemSummary> toAppend = items;
        if (dedupeByUrl)
        {
            var urlColIndex = await GetColumnIndexByHeaderAsync(sheetName, "ItemWebUrl").ConfigureAwait(false);
            if (urlColIndex >= 0)
            {
                var endColLetter = ColumnIndexToLetter(urlColIndex + 1);
                var get = _sheetService.Spreadsheets.Values.Get(spreadsheetId, $"{sheetName}!A2:{endColLetter}");
                var resp = await get.ExecuteAsync().ConfigureAwait(false);
                var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (resp.Values != null)
                {
                    foreach (var row in resp.Values)
                    {
                        if (row.Count > urlColIndex)
                        {
                            var url = row[urlColIndex]?.ToString() ?? string.Empty;
                            if (!string.IsNullOrEmpty(url)) existing.Add(url);
                        }
                    }
                }
                toAppend = items.Where(i => !string.IsNullOrEmpty(i.ItemWebUrl) && !existing.Contains(i.ItemWebUrl));
            }
        }
        var rows = new List<IList<object>>();
        foreach (var it in toAppend)
            rows.Add(FormatItemRow(it, centralZone));
        if (rows.Count == 0) return 0;
        var vr = new ValueRange { Values = rows };
        var append = _sheetService.Spreadsheets.Values.Append(vr, spreadsheetId, $"{sheetName}!A1");
        append.ValueInputOption = SpreadsheetsResource.ValuesResource.AppendRequest.ValueInputOptionEnum.USERENTERED;
        append.InsertDataOption = SpreadsheetsResource.ValuesResource.AppendRequest.InsertDataOptionEnum.INSERTROWS;
        await append.ExecuteAsync().ConfigureAwait(false);
        return rows.Count;
    }


    /// <summary>
    /// Appends items to the specified sheet using a row selector and optional deduplication.
    /// </summary>
    /// <typeparam name="T">Type of item.</typeparam>
    /// <param name="items">Items to append.</param>
    /// <param name="sheetName">Sheet name.</param>
    /// <param name="rowSelector">Function to select row values.</param>
    /// <param name="dedupeByUrl">Whether to deduplicate by URL.</param>
    /// <param name="urlSelector">Function to select URL for deduplication.</param>
    /// <returns>Number of rows appended.</returns>
    public async Task<int> AppendItemsAsync<T>(List<T> items, string sheetName, Func<T, IList<object>> rowSelector, bool dedupeByUrl = false, Func<T, string>? urlSelector = null)
    {
        if (items is null) throw new ArgumentNullException(nameof(items));
        if (string.IsNullOrWhiteSpace(sheetName)) throw new ArgumentException("Sheet name is required", nameof(sheetName));
        var spreadsheetId = _config.sheetid;
        IEnumerable<T> toAppend = items;
        if (dedupeByUrl && urlSelector != null)
        {
            var urlColIndex = await GetColumnIndexByHeaderAsync(sheetName, "ItemWebUrl").ConfigureAwait(false);
            if (urlColIndex >= 0)
            {
                var endColLetter = ColumnIndexToLetter(urlColIndex + 1);
                var get = _sheetService.Spreadsheets.Values.Get(spreadsheetId, $"{sheetName}!A2:{endColLetter}");
                var resp = await get.ExecuteAsync().ConfigureAwait(false);
                var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (resp.Values != null)
                {
                    foreach (var row in resp.Values)
                    {
                        if (row.Count > urlColIndex)
                        {
                            var url = row[urlColIndex]?.ToString() ?? string.Empty;
                            if (!string.IsNullOrEmpty(url)) existing.Add(url);
                        }
                    }
                }
                toAppend = items.Where(i => !string.IsNullOrEmpty(urlSelector(i)) && !existing.Contains(urlSelector(i)));
            }
        }
        var rows = toAppend.Select(rowSelector).ToList();
        if (rows.Count == 0) return 0;
        var vr = new ValueRange { Values = rows };
        var append = _sheetService.Spreadsheets.Values.Append(vr, spreadsheetId, $"{sheetName}!A1");
        append.ValueInputOption = SpreadsheetsResource.ValuesResource.AppendRequest.ValueInputOptionEnum.USERENTERED;
        append.InsertDataOption = SpreadsheetsResource.ValuesResource.AppendRequest.InsertDataOptionEnum.INSERTROWS;
        await append.ExecuteAsync().ConfigureAwait(false);
        return rows.Count;
    }


    /// <summary>
    /// Deletes rows by their indices in the specified sheet.
    /// </summary>
    /// <param name="sheetName">Sheet name.</param>
    /// <param name="rowNumbers">Row numbers to delete.</param>
    /// <returns>Number of rows deleted.</returns>
    public async Task<int> DeleteRowsAsync(string sheetName, IEnumerable<int> rowNumbers)
    {
        if (string.IsNullOrWhiteSpace(sheetName)) throw new ArgumentException("Sheet name is required", nameof(sheetName));
        if (rowNumbers is null) throw new ArgumentNullException(nameof(rowNumbers));
        var ids = rowNumbers.Distinct().Where(n => n >= 1).OrderBy(n => n).ToList();
        if (ids.Count == 0) return 0;
        var spreadsheetId = _config.sheetid;
        var sheetId = await GetSheetIdAsync(sheetName).ConfigureAwait(false);
        if (!sheetId.HasValue) throw new InvalidOperationException($"Sheet '{sheetName}' not found.");
        var ranges = CompressIndicesToRanges(ids);
        var requests = new List<Request>();
        foreach (var (start, end) in ranges)
        {
            requests.Add(new Request
            {
                DeleteDimension = new DeleteDimensionRequest
                {
                    Range = new DimensionRange
                    {
                        SheetId = sheetId.Value,
                        Dimension = "ROWS",
                        StartIndex = start - 1,
                        EndIndex = end
                    }
                }
            });
        }
        var batch = new BatchUpdateSpreadsheetRequest { Requests = requests };
        var op = _sheetService.Spreadsheets.BatchUpdate(batch, spreadsheetId);
        await op.ExecuteAsync().ConfigureAwait(false);
        return ids.Count;
    }


    /// <summary>
    /// Deletes rows from the sheet by matching URLs.
    /// </summary>
    /// <param name="sheetName">Sheet name.</param>
    /// <param name="urls">URLs to match for deletion.</param>
    /// <returns>Number of rows deleted.</returns>
    public async Task<int> DeleteRowsByUrlAsync(string sheetName, IEnumerable<string> urls)
    {
        if (urls is null) throw new ArgumentNullException(nameof(urls));
        var urlSet = new HashSet<string>(urls.Where(u => !string.IsNullOrWhiteSpace(u)), StringComparer.OrdinalIgnoreCase);
        if (urlSet.Count == 0) return 0;
        var spreadsheetId = _config.sheetid;
        var urlColIndex = await GetColumnIndexByHeaderAsync(sheetName, "ItemWebUrl").ConfigureAwait(false);
        if (urlColIndex < 0) throw new InvalidOperationException($"Header 'ItemWebUrl' not found in sheet '{sheetName}'.");
        var endColLetter = ColumnIndexToLetter(urlColIndex + 1);
        var get = _sheetService.Spreadsheets.Values.Get(spreadsheetId, $"{sheetName}!A2:{endColLetter}");
        var resp = await get.ExecuteAsync().ConfigureAwait(false);
        var toDelete = new List<int>();
        if (resp.Values != null)
        {
            int row = 2;
            foreach (var r in resp.Values)
            {
                if (r.Count > urlColIndex)
                {
                    var url = r[urlColIndex]?.ToString() ?? string.Empty;
                    if (!string.IsNullOrEmpty(url) && urlSet.Contains(url)) toDelete.Add(row);
                }
                row++;
            }
        }
        if (toDelete.Count == 0) return 0;
        return await DeleteRowsAsync(sheetName, toDelete).ConfigureAwait(false);
    }


    /// <summary>
    /// Updates rows by their indices in the specified sheet.
    /// </summary>
    /// <param name="sheetName">Sheet name.</param>
    /// <param name="updates">Row updates.</param>
    /// <returns>Number of rows updated.</returns>
    public async Task<int> UpdateRowsByIndexAsync(string sheetName, IEnumerable<RowUpdate> updates)
    {
        if (string.IsNullOrWhiteSpace(sheetName)) throw new ArgumentException("Sheet name is required", nameof(sheetName));
        if (updates is null) throw new ArgumentNullException(nameof(updates));
        var data = new List<ValueRange>();
        foreach (var up in updates)
        {
            if (up.RowNumber < 1) continue;
            var colCount = up.Values?.Count ?? 0;
            if (colCount == 0) continue;
            var endColLetter = ColumnIndexToLetter(colCount);
            var range = $"{sheetName}!A{up.RowNumber}:{endColLetter}{up.RowNumber}";
            data.Add(new ValueRange
            {
                Range = range,
                Values = new List<IList<object>> { up.Values }
            });
        }
        if (data.Count == 0) return 0;
        var body = new BatchUpdateValuesRequest
        {
            ValueInputOption = "USER_ENTERED",
            Data = data
        };
        var op = _sheetService.Spreadsheets.Values.BatchUpdate(body, _config.sheetid);
        var res = await op.ExecuteAsync().ConfigureAwait(false);
        return res.TotalUpdatedRows ?? 0;
    }


    /// <summary>
    /// Updates item rows in the sheet by matching URLs.
    /// </summary>
    /// <param name="sheetName">Sheet name.</param>
    /// <param name="items">Items to update.</param>
    /// <param name="centralZone">Time zone for date conversion.</param>
    /// <returns>Number of rows updated.</returns>
    public async Task<int> UpdateItemsByUrlAsync(string sheetName, IEnumerable<ItemSummary> items, TimeZoneInfo centralZone)
    {
        if (items is null) throw new ArgumentNullException(nameof(items));
        var list = items.Where(i => !string.IsNullOrWhiteSpace(i.ItemWebUrl)).ToList();
        if (list.Count == 0) return 0;
        var spreadsheetId = _config.sheetid;
        var urlColIndex = await GetColumnIndexByHeaderAsync(sheetName, "ItemWebUrl").ConfigureAwait(false);
        if (urlColIndex < 0) throw new InvalidOperationException($"Header 'ItemWebUrl' not found in sheet '{sheetName}'.");
        var endColLetter = ColumnIndexToLetter(Math.Max(urlColIndex + 1, 10));
        var get = _sheetService.Spreadsheets.Values.Get(spreadsheetId, $"{sheetName}!A2:{endColLetter}");
        var resp = await get.ExecuteAsync().ConfigureAwait(false);
        var rowByUrl = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (resp.Values != null)
        {
            int row = 2;
            foreach (var r in resp.Values)
            {
                if (r.Count > urlColIndex)
                {
                    var url = r[urlColIndex]?.ToString() ?? string.Empty;
                    if (!string.IsNullOrEmpty(url) && !rowByUrl.ContainsKey(url)) rowByUrl[url] = row;
                }
                row++;
            }
        }
        var data = new List<ValueRange>();
        foreach (var item in list)
        {
            if (!rowByUrl.TryGetValue(item.ItemWebUrl!, out var rowNum)) continue;
            var values = FormatItemRow(item, centralZone);
            var endLetter = ColumnIndexToLetter(values.Count);
            var range = $"{sheetName}!A{rowNum}:{endLetter}{rowNum}";
            data.Add(new ValueRange { Range = range, Values = new List<IList<object>> { values } });
        }
        if (data.Count == 0) return 0;
        var body = new BatchUpdateValuesRequest { ValueInputOption = "USER_ENTERED", Data = data };
        var op = _sheetService.Spreadsheets.Values.BatchUpdate(body, spreadsheetId);
        var res = await op.ExecuteAsync().ConfigureAwait(false);
        return res.TotalUpdatedRows ?? 0;
    }


    /// <summary>
    /// Appends rows to the specified sheet.
    /// </summary>
    /// <param name="sheetName">Sheet name.</param>
    /// <param name="rows">Rows to append.</param>
    /// <returns>Number of rows appended.</returns>
    public async Task<int> AppendRowsAsync(string sheetName, IList<IList<object>> rows)
    {
        if (rows == null || rows.Count == 0) return 0;
        if (string.IsNullOrWhiteSpace(sheetName)) throw new ArgumentException("Sheet name is required", nameof(sheetName));
        var spreadsheetId = _config.sheetid;
        var vr = new ValueRange { Values = rows };
        var append = _sheetService.Spreadsheets.Values.Append(vr, spreadsheetId, $"{sheetName}!A1");
        append.ValueInputOption = SpreadsheetsResource.ValuesResource.AppendRequest.ValueInputOptionEnum.USERENTERED;
        append.InsertDataOption = SpreadsheetsResource.ValuesResource.AppendRequest.InsertDataOptionEnum.INSERTROWS;
        await append.ExecuteAsync().ConfigureAwait(false);
        return rows.Count;
    }


    /// <summary>
    /// Deletes all rows except the header in the specified sheet.
    /// </summary>
    /// <param name="sheetName">Sheet name.</param>
    /// <returns>Number of rows deleted.</returns>
    public async Task<int> DeleteAllRowsExceptHeaderAsync(string sheetName)
    {
        // Get all rows (excluding header)
        var allRows = await GetAllRowsAsync<object>(sheetName, row => row);
        if (allRows.Count == 0) return 0;

        var rowCount = allRows.Count;
        if (rowCount > 1)
        {
            // Leave the last row, delete rows 2 to rowCount
            var rowsToDelete = Enumerable.Range(2, rowCount - 2);
            return await DeleteRowsAsync(sheetName, rowsToDelete);
        }
        return 0;
    }


    /// <summary>
    /// Parses the year from a card title.
    /// </summary>
    /// <param name="title">Item title.</param>
    /// <returns>Year as string, or empty string.</returns>
    public static string ParseCardYear(string title)
    {
        if (string.IsNullOrWhiteSpace(title)) return "";
        var yearMatch = Regex.Match(title, @"\b(\d{4})(?:-(\d{2}))?\b");
        if (yearMatch.Success) return yearMatch.Groups[1].Value;
        return "";
    }


    /// <summary>
    /// Gets the sheet ID for a given sheet name.
    /// </summary>
    /// <param name="sheetName">Sheet name.</param>
    /// <returns>Sheet ID or null.</returns>
    private async Task<int?> GetSheetIdAsync(string sheetName)
    {
        var ss = await _sheetService.Spreadsheets.Get(_config.sheetid).ExecuteAsync().ConfigureAwait(false);
        var sheet = ss.Sheets?.FirstOrDefault(s => string.Equals(s.Properties?.Title, sheetName, StringComparison.OrdinalIgnoreCase));
        return sheet?.Properties?.SheetId;
    }


    /// <summary>
    /// Gets the column index for a header name in the sheet.
    /// </summary>
    /// <param name="sheetName">Sheet name.</param>
    /// <param name="headerName">Header name.</param>
    /// <returns>Column index or -1.</returns>
    private async Task<int> GetColumnIndexByHeaderAsync(string sheetName, string headerName)
    {
        var get = _sheetService.Spreadsheets.Values.Get(_config.sheetid, $"{sheetName}!A1:Z1");
        var resp = await get.ExecuteAsync().ConfigureAwait(false);
        var headers = resp.Values?.FirstOrDefault();
        if (headers == null) return -1;
        for (int i = 0; i < headers.Count; i++)
        {
            if (string.Equals(headers[i]?.ToString(), headerName, StringComparison.OrdinalIgnoreCase)) return i;
        }
        return -1;
    }


    /// <summary>
    /// Converts a 1-based column index to a column letter (e.g., 1 -> A).
    /// </summary>
    /// <param name="columnIndex1Based">Column index (1-based).</param>
    /// <returns>Column letter.</returns>
    private static string ColumnIndexToLetter(int columnIndex1Based)
    {
        if (columnIndex1Based <= 0) throw new ArgumentOutOfRangeException(nameof(columnIndex1Based));
        var dividend = columnIndex1Based;
        string columnName = string.Empty;
        while (dividend > 0)
        {
            var modulo = (dividend - 1) % 26;
            columnName = Convert.ToChar('A' + modulo) + columnName;
            dividend = (dividend - modulo) / 26;
        }
        return columnName;
    }


    /// <summary>
    /// Compresses a sorted list of indices into ranges.
    /// </summary>
    /// <param name="sortedDistinct">Sorted distinct indices.</param>
    /// <returns>List of (start, end) ranges.</returns>
    private static List<(int start, int end)> CompressIndicesToRanges(IList<int> sortedDistinct)
    {
        var result = new List<(int, int)>();
        if (sortedDistinct.Count == 0) return result;
        int start = sortedDistinct[0];
        int prev = start;
        for (int i = 1; i < sortedDistinct.Count; i++)
        {
            int current = sortedDistinct[i];
            if (current == prev + 1)
            {
                prev = current;
                continue;
            }
            result.Add((start, prev + 1));
            start = prev = current;
        }
        result.Add((start, prev + 1));
        return result;
    }


    /// <summary>
    /// Formats an ItemSummary into a row for Google Sheets.
    /// </summary>
    /// <param name="item">ItemSummary object.</param>
    /// <param name="centralZone">Time zone for date conversion.</param>
    /// <returns>List of objects representing the row.</returns>
    private IList<object> FormatItemRow(ItemSummary item, TimeZoneInfo centralZone)
    {
        string title = item.Title?.Replace("\"", "\"\"") ?? "";
        string year = ParseCardYear(item.Title);
        string price = item.CurrentBidPrice?.Value ?? "";
        string bidCount = item.BidCount?.ToString() ?? "";
        DateTime centralEndDate = TimeZoneInfo.ConvertTimeFromUtc(item.ItemEndDate, centralZone);
        string endDate = centralEndDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        string endTime = centralEndDate.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
        string numbered = item.Title != null && item.Title.Contains("/") ? "Yes" : "No";
        int outOf = 999999;
        if (!string.IsNullOrEmpty(item.Title))
        {
            var match = Regex.Match(item.Title, @"#(?:\d+)?/(\d+)");
            if (match.Success && int.TryParse(match.Groups[1].Value, out int parsed))
            {
                outOf = parsed;
            }
            else
            {
                match = Regex.Match(item.Title, @"#\d+");
                if (match.Success)
                {
                    outOf = 999999;
                }
            }
        }
        string rookie = AuctionItemUtil.ParseRC(item.Title);
        string url = item.ItemWebUrl ?? "";
        if (url.Contains(",") || url.Contains("\""))
            url = $"\"{url.Replace("\"", "\"\"")}\"";
        return new List<object> { title, year, price, bidCount, endDate, endTime, numbered, outOf.ToString(), rookie, url };
    }


    public async Task<int> ClearAllFiltersAsync()
    {
        // Get spreadsheet metadata to list all sheets
        var spreadsheet = await _sheetService.Spreadsheets.Get(_config.sheetid).ExecuteAsync();
        var requests = new List<Request>();

        foreach (var sheet in spreadsheet.Sheets)
        {
            var sheetId = sheet.Properties.SheetId;

            // Remove basic filter
            requests.Add(new Request
            {
                ClearBasicFilter = new ClearBasicFilterRequest
                {
                    SheetId = sheetId
                }
            });
        }

        if (requests.Count == 0)
            return 0;

        var batchRequest = new BatchUpdateSpreadsheetRequest
        {
            Requests = requests
        };

        var response = await _sheetService.Spreadsheets.BatchUpdate(batchRequest, _config.sheetid).ExecuteAsync();
        return requests.Count;
    }


    public async Task SetBasicFilterAsync(string sheetName, int headerRowIndex = 0)
    {
        var sheetId = await GetSheetIdAsync(sheetName);
        if (sheetId == null)
            throw new InvalidOperationException($"Sheet '{sheetName}' not found.");

        // Get header row to determine column indices
        var get = _sheetService.Spreadsheets.Values.Get(_config.sheetid, $"{sheetName}!A1:Z1");
        var resp = await get.ExecuteAsync().ConfigureAwait(false);
        var headers = resp.Values?.FirstOrDefault()?.Select(h => h?.ToString() ?? "").ToList();
        if (headers == null || headers.Count == 0)
            throw new InvalidOperationException("No header row found.");

        int bidCountCol = headers.FindIndex(h => h.Equals("BidCount", StringComparison.OrdinalIgnoreCase));
        int endDateCol = headers.FindIndex(h => h.Equals("EndDate", StringComparison.OrdinalIgnoreCase));
        int outOfCol = headers.FindIndex(h => h.Equals("OutOf", StringComparison.OrdinalIgnoreCase));
        int columnCount = headers.Count;

        // Get row count (including header)
        var allRows = await GetAllRowsAsync<object>(sheetName, r => r).ConfigureAwait(false);
        int rowCount = allRows.Count + 1; // +1 for header

        // Central Time today as yyyy-MM-dd
        var centralZone = TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time");
        var todayCentral = TimeZoneInfo.ConvertTime(DateTime.UtcNow, centralZone).Date.ToString("yyyy-MM-dd");

        // Build filterSpecs list
        var filterSpecs = new List<FilterSpec>();

        // Filter: BidCount == 0
        if (bidCountCol >= 0)
        {
            filterSpecs.Add(new FilterSpec
            {
                ColumnIndex = bidCountCol,
                FilterCriteria = new FilterCriteria
                {
                    Condition = new BooleanCondition
                    {
                        Type = "NUMBER_EQ",
                        Values = new List<ConditionValue>
                        {
                            new ConditionValue { UserEnteredValue = "0" }
                        }
                    }
                }
            });
        }

        // Filter: EndDate is exactly today's date in CST
        if (endDateCol >= 0)
        {
            filterSpecs.Add(new FilterSpec
            {
                ColumnIndex = endDateCol,
                FilterCriteria = new FilterCriteria
                {
                    Condition = new BooleanCondition
                    {
                        Type = "DATE_EQ",
                        Values = new List<ConditionValue>
                        {
                            new ConditionValue { UserEnteredValue = todayCentral }
                        }
                    }
                }
            });
        }

        // Sort: OutOf ascending
        var sortSpecs = new List<SortSpec>();
        if (outOfCol >= 0)
        {
            sortSpecs.Add(new SortSpec
            {
                DimensionIndex = outOfCol,
                SortOrder = "ASCENDING"
            });
        }

        var basicFilter = new BasicFilter
        {
            Range = new GridRange
            {
                SheetId = sheetId.Value,
                StartRowIndex = headerRowIndex,
                EndRowIndex = rowCount,
                StartColumnIndex = 0,
                EndColumnIndex = columnCount
            },
            FilterSpecs = filterSpecs.Count > 0 ? filterSpecs : null,
            SortSpecs = sortSpecs.Count > 0 ? sortSpecs : null
        };

        var filterRequest = new Request
        {
            SetBasicFilter = new SetBasicFilterRequest
            {
                Filter = basicFilter
            }
        };

        var batchRequest = new BatchUpdateSpreadsheetRequest
        {
            Requests = new List<Request> { filterRequest }
        };

        await _sheetService.Spreadsheets.BatchUpdate(batchRequest, _config.sheetid).ExecuteAsync();
    }


    /// <summary>
    /// Test: Sets a basic filter on the specified sheet for BidCount == 0 only,
    /// using filterSpecs (recommended by Google Sheets API) instead of criteria.
    /// </summary>
    /// <param name="sheetName">Sheet/tab name.</param>
    public async Task SetBasicFilterBidCountZeroAsync(string sheetName)
    {
        var sheetId = await GetSheetIdAsync(sheetName);
        if (sheetId == null)
            throw new InvalidOperationException($"Sheet '{sheetName}' not found.");

        // Get header row to determine column indices
        var get = _sheetService.Spreadsheets.Values.Get(_config.sheetid, $"{sheetName}!A1:Z1");
        var resp = await get.ExecuteAsync().ConfigureAwait(false);
        var headers = resp.Values?.FirstOrDefault()?.Select(h => h?.ToString() ?? "").ToList();
        if (headers == null || headers.Count == 0)
            throw new InvalidOperationException("No header row found.");

        int bidCountCol = headers.FindIndex(h => h.Equals("BidCount", StringComparison.OrdinalIgnoreCase));
        int columnCount = headers.Count;

        // Get all data rows (excluding header)
        var allRows = await GetAllRowsAsync<object>(sheetName, r => r).ConfigureAwait(false);
        int dataRowCount = allRows.Count;
        if (dataRowCount == 0 || bidCountCol < 0)
            return; // No data or no BidCount column, do not set filter

        // Build filterSpecs for BidCount == 0
        var filterSpecs = new List<FilterSpec>
        {
            new FilterSpec
            {
                ColumnIndex = bidCountCol,
                FilterCriteria = new FilterCriteria
                {
                    Condition = new BooleanCondition
                    {
                        Type = "NUMBER_EQ",
                        Values = new List<ConditionValue>
                        {
                            new ConditionValue { UserEnteredValue = "0" }
                        }
                    }
                }
            }
        };

        var basicFilter = new BasicFilter
        {
            Range = new GridRange
            {
                SheetId = sheetId.Value,
                StartRowIndex = 0, // header at row 0
                EndRowIndex = dataRowCount + 1, // header + data
                StartColumnIndex = 0,
                EndColumnIndex = columnCount
            },
            FilterSpecs = filterSpecs
        };

        var filterRequest = new Request
        {
            SetBasicFilter = new SetBasicFilterRequest
            {
                Filter = basicFilter
            }
        };

        var batchRequest = new BatchUpdateSpreadsheetRequest
        {
            Requests = new List<Request> { filterRequest }
        };

        await _sheetService.Spreadsheets.BatchUpdate(batchRequest, _config.sheetid).ExecuteAsync();
    }
    #endregion
}