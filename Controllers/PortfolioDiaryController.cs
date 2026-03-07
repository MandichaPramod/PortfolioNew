using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using PortfolioNew.Models;
using PortfolioNew.Services;
using System.Globalization;

namespace PortfolioNew.Controllers
{
    public class PortfolioDiaryController : Controller
    {
        private const string BreakdownCacheKey = "sheet:Breakdown:A3:P";
        private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(15);
        private readonly IMemoryCache _memoryCache;

        public PortfolioDiaryController(IMemoryCache memoryCache)
        {
            _memoryCache = memoryCache;
        }

        public IActionResult Index(string? account = null, string? tag = null, string? symbol = null)
        {
            var rows = ParseRows(GetSheetDataCached(BreakdownCacheKey, "Breakdown", "A3:P"))
                .OrderBy(x => x.Date)
                .ToList();

            var accounts = rows.Select(r => r.Account).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList();
            var tags = rows.Select(r => r.Tag).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList();

            var accountFilter = string.IsNullOrWhiteSpace(account) ? "All" : account.Trim();
            var tagFilter = string.IsNullOrWhiteSpace(tag) ? "All" : tag.Trim();
            var symbolFilter = string.IsNullOrWhiteSpace(symbol) ? "" : symbol.Trim();

            var filteredRows = rows.Where(r =>
                    (accountFilter.Equals("All", StringComparison.OrdinalIgnoreCase) || string.Equals(r.Account, accountFilter, StringComparison.OrdinalIgnoreCase)) &&
                    (tagFilter.Equals("All", StringComparison.OrdinalIgnoreCase) || string.Equals(r.Tag, tagFilter, StringComparison.OrdinalIgnoreCase)) &&
                    (string.IsNullOrWhiteSpace(symbolFilter) || r.Symbol.Contains(symbolFilter, StringComparison.OrdinalIgnoreCase) || r.Name.Contains(symbolFilter, StringComparison.OrdinalIgnoreCase)))
                .OrderBy(x => x.Date)
                .ToList();

            var portfolios = new List<string>();
            foreach (var r in filteredRows)
            {
                if (!string.IsNullOrWhiteSpace(r.Portfolio) && !portfolios.Contains(r.Portfolio))
                {
                    portfolios.Add(r.Portfolio);
                }
            }

            var yearly = filteredRows
                .GroupBy(x => x.Date.Year)
                .OrderByDescending(g => g.Key)
                .Select(g =>
                {
                    var values = portfolios.ToDictionary(p => p, _ => 0.0);
                    foreach (var r in g)
                    {
                        if (!string.IsNullOrWhiteSpace(r.Portfolio))
                        {
                            values[r.Portfolio] = values[r.Portfolio] + r.TotalCost;
                        }
                    }
                    var top = values.OrderByDescending(v => v.Value).FirstOrDefault();
                    return new PortfolioDiaryMatrixRow
                    {
                        Year = g.Key,
                        Month = 0,
                        PeriodLabel = g.Key.ToString(),
                        PortfolioValues = values,
                        Total = values.Values.Sum(),
                        TopPortfolio = top.Key ?? "",
                        TopValue = top.Value
                    };
                })
                .ToList();

            var monthly = filteredRows
                .GroupBy(x => new { x.Date.Year, x.Date.Month })
                .OrderByDescending(g => g.Key.Year)
                .ThenByDescending(g => g.Key.Month)
                .Select(g =>
                {
                    var values = portfolios.ToDictionary(p => p, _ => 0.0);
                    foreach (var r in g)
                    {
                        if (!string.IsNullOrWhiteSpace(r.Portfolio))
                        {
                            values[r.Portfolio] = values[r.Portfolio] + r.TotalCost;
                        }
                    }
                    var top = values.OrderByDescending(v => v.Value).FirstOrDefault();
                    return new PortfolioDiaryMatrixRow
                    {
                        Year = g.Key.Year,
                        Month = g.Key.Month,
                        PeriodLabel = $"{g.Key.Month}-{g.Key.Year}",
                        PortfolioValues = values,
                        Total = values.Values.Sum(),
                        TopPortfolio = top.Key ?? "",
                        TopValue = top.Value
                    };
                })
                .ToList();

            ApplyDeltas(yearly);
            ApplyDeltas(monthly);

            return View(new PortfolioDiaryResponseModel
            {
                AccountFilter = accountFilter,
                TagFilter = tagFilter,
                SymbolFilter = symbolFilter,
                Accounts = accounts,
                Tags = tags,
                Portfolios = portfolios,
                Transactions = filteredRows.Select(x => new PortfolioDiaryTransactionRow
                {
                    Symbol = x.Symbol,
                    Name = x.Name,
                    Account = x.Account,
                    Tag = x.Tag,
                    Portfolio = x.Portfolio,
                    Date = x.Date,
                    TotalCost = x.TotalCost,
                    Quantity = x.Quantity,
                    BuyPrice = x.BuyPrice
                }).ToList(),
                YearlyRows = yearly,
                MonthlyRows = monthly,
                GrandTotal = filteredRows.Sum(x => x.TotalCost)
            });
        }

        private static void ApplyDeltas(List<PortfolioDiaryMatrixRow> rowsDesc)
        {
            for (int i = 0; i < rowsDesc.Count; i++)
            {
                var current = rowsDesc[i];
                if (i == rowsDesc.Count - 1)
                {
                    current.Delta = 0;
                    current.DeltaPercent = 0;
                    continue;
                }
                var prev = rowsDesc[i + 1];
                current.Delta = current.Total - prev.Total;
                current.DeltaPercent = prev.Total > 0 ? (current.Delta * 100 / prev.Total) : 0;
            }
        }

        private sealed class DiaryRow
        {
            public string Symbol { get; set; } = "";
            public string Name { get; set; } = "";
            public string Account { get; set; } = "";
            public string Tag { get; set; } = "";
            public DateTime Date { get; set; }
            public double TotalCost { get; set; }
            public string Portfolio { get; set; } = "";
            public double Quantity { get; set; }
            public double BuyPrice { get; set; }
        }

        private static List<DiaryRow> ParseRows(List<List<object>> rows)
        {
            static double ParseDouble(object? value)
            {
                var text = value?.ToString()?.Trim();
                if (string.IsNullOrWhiteSpace(text))
                {
                    return 0;
                }
                text = text.Replace(",", "").Replace("₹", "").Replace("â‚¹", "").Replace("$", "");
                text = new string(text.Where(c => char.IsDigit(c) || c == '.' || c == '-').ToArray());
                if (string.IsNullOrWhiteSpace(text) || text == "-" || text == ".")
                {
                    return 0;
                }
                return double.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;
            }

            static DateTime ParseDate(object? value)
            {
                var text = value?.ToString()?.Trim();
                return DateTime.TryParse(text, out var d) ? d : DateTime.MinValue;
            }

            var list = new List<DiaryRow>();
            foreach (var row in rows)
            {
                while (row.Count < 16) row.Add("");
                if (row.Count < 16) continue;
                var symbol = row[0]?.ToString()?.Trim() ?? "";
                var name = row[1]?.ToString()?.Trim() ?? "";
                if (string.IsNullOrWhiteSpace(symbol) || string.IsNullOrWhiteSpace(name) ||
                    string.Equals(symbol, "SYMBOL", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(name, "Name", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                list.Add(new DiaryRow
                {
                    Symbol = symbol,
                    Name = name,
                    Account = row[2]?.ToString()?.Trim() ?? "",
                    Tag = row[12]?.ToString()?.Trim() ?? "",
                    Date = ParseDate(row[3]),
                    Quantity = ParseDouble(row[4]),
                    BuyPrice = ParseDouble(row[5]),
                    TotalCost = ParseDouble(row[6]),
                    Portfolio = row[11]?.ToString()?.Trim() ?? ""
                });
            }

            return list;
        }

        private List<List<object>> GetSheetDataCached(string cacheKey, string sheet, string range)
        {
            if (!_memoryCache.TryGetValue(cacheKey, out List<List<object>>? cachedData))
            {
                var fetched = GoogleSheetsService.GetDataFromSheet(sheet, range);
                cachedData = CloneSheetData(fetched);
                _memoryCache.Set(cacheKey, cachedData, new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = CacheDuration
                });
            }

            return CloneSheetData(cachedData);
        }

        private static List<List<object>> CloneSheetData(IEnumerable<IEnumerable<object>>? source)
        {
            if (source == null)
            {
                return new List<List<object>>();
            }

            return source.Select(row => row.Select(cell => cell ?? "").ToList()).ToList();
        }
    }
}
