using System.Text.RegularExpressions;
using eBay_API.Models.Config;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;



public class GoogleDriveService
{
    #region PROPERTIES
    private readonly GoogleDriveConfig _config;
    private Google.Apis.Sheets.v4.SheetsService _sheetService;
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


    public async Task<int> ClearFiltersAsync(string tabName)
    {
        // Get spreadsheet metadata to list all sheets
        var spreadsheet = await _sheetService.Spreadsheets.Get(_config.sheetid).ExecuteAsync();
        var requests = new List<Request>();

        var tabId = await GetSheetIdAsync(tabName);
        if (tabId == null)
            throw new InvalidOperationException($"Tab '{tabName}' not found.");

        requests.Add(new Request
        {
            ClearBasicFilter = new ClearBasicFilterRequest
            {
                SheetId = tabId
            }
        });

        var batchRequest = new BatchUpdateSpreadsheetRequest
        {
            Requests = requests
        };

        var response = await _sheetService.Spreadsheets.BatchUpdate(batchRequest, _config.sheetid).ExecuteAsync();
        return requests.Count;
    }


    public async Task SetBasicOrdersFilterAsync(string tabName, int headerRowIndex = 0)
    {
        var tabId = await GetSheetIdAsync(tabName);
        if (tabId == null)
            throw new InvalidOperationException($"Tab '{tabName}' not found.");

        // Get header row to determine column indices
        var get = _sheetService.Spreadsheets.Values.Get(_config.sheetid, $"{tabName}!A1:Z1");
        var resp = await get.ExecuteAsync().ConfigureAwait(false);
        var headers = resp.Values?.FirstOrDefault()?.Select(h => h?.ToString() ?? "").ToList();
        if (headers == null || headers.Count == 0)
            throw new InvalidOperationException("No header row found.");

        // Find the "Created" column index
        int createdCol = headers.FindIndex(h => h.Equals("Created", StringComparison.OrdinalIgnoreCase));
        int columnCount = headers.Count;

        // Get all data rows (excluding header)
        var allRows = await GetAllRowsAsync<object>(tabName, r => r).ConfigureAwait(false);
        int dataRowCount = allRows.Count;

        // Build sortSpecs for "Created" descending
        var sortSpecs = new List<SortSpec>();
        if (createdCol >= 0)
        {
            sortSpecs.Add(new SortSpec
            {
                DimensionIndex = createdCol,
                SortOrder = "DESCENDING"
            });
        }

        var basicFilter = new BasicFilter
        {
            Range = new GridRange
            {
                SheetId = tabId.Value,
                StartRowIndex = headerRowIndex,
                EndRowIndex = dataRowCount + 1, // header + data
                StartColumnIndex = 0,
                EndColumnIndex = columnCount
            },
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
    #endregion
}