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

        public IActionResult Index()
        {
            var rows = ParseRows(GetSheetDataCached(BreakdownCacheKey, "Breakdown", "A3:P"))
                .OrderBy(x => x.Date)
                .ToList();

            var portfolios = new List<string>();
            foreach (var r in rows)
            {
                if (!string.IsNullOrWhiteSpace(r.Portfolio) && !portfolios.Contains(r.Portfolio))
                {
                    portfolios.Add(r.Portfolio);
                }
            }

            var yearly = rows
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
                    return new PortfolioDiaryMatrixRow
                    {
                        PeriodLabel = g.Key.ToString(),
                        PortfolioValues = values,
                        Total = values.Values.Sum()
                    };
                })
                .ToList();

            var monthly = rows
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
                    return new PortfolioDiaryMatrixRow
                    {
                        PeriodLabel = $"{g.Key.Month}-{g.Key.Year}",
                        PortfolioValues = values,
                        Total = values.Values.Sum()
                    };
                })
                .ToList();

            return View(new PortfolioDiaryResponseModel
            {
                Portfolios = portfolios,
                YearlyRows = yearly,
                MonthlyRows = monthly,
                GrandTotal = rows.Sum(x => x.TotalCost)
            });
        }

        private sealed class DiaryRow
        {
            public DateTime Date { get; set; }
            public double TotalCost { get; set; }
            public string Portfolio { get; set; } = "";
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
                    Date = ParseDate(row[3]),
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
