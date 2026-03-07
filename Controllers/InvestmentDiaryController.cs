using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using PortfolioNew.Models;
using PortfolioNew.Services;
using System.Globalization;

namespace PortfolioNew.Controllers
{
    public class InvestmentDiaryController : Controller
    {
        private const string BreakdownCacheKey = "sheet:Breakdown:A3:P";
        private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(15);
        private readonly IMemoryCache _memoryCache;

        public InvestmentDiaryController(IMemoryCache memoryCache)
        {
            _memoryCache = memoryCache;
        }

        public IActionResult Index(bool bonusSplitAdjusted = false)
        {
            var rows = ParseRows(GetSheetDataCached(BreakdownCacheKey, "Breakdown", "A3:P"))
                .OrderBy(x => x.Date)
                .ToList();

            if (bonusSplitAdjusted)
            {
                ApplyBonusSplitAdjustments(rows);
                rows.RemoveAll(x => string.Equals((x.Tag ?? "").Trim(), "Bonus", StringComparison.OrdinalIgnoreCase)
                    || string.Equals((x.Tag ?? "").Trim(), "Split", StringComparison.OrdinalIgnoreCase)
                    || string.Equals((x.Tag ?? "").Trim(), "Dividend Reinvestment", StringComparison.OrdinalIgnoreCase));
            }

            var yearly = rows
                .GroupBy(x => x.Date.Year)
                .Select(g => new InvestmentDiaryPeriodSummary
                {
                    PeriodDate = new DateTime(g.Key, 1, 1),
                    PeriodLabel = g.Key.ToString(),
                    TotalCost = g.Sum(x => x.TotalCost),
                    CurrentValue = g.Sum(x => x.CurrentValue),
                    Dividend = g.Sum(x => x.Dividend),
                    DividendReinvestment = g.Sum(x => x.DividendReinvestment),
                    Rows = g.OrderBy(x => x.Date).ToList()
                })
                .OrderByDescending(x => x.PeriodDate)
                .ToList();

            var monthly = rows
                .GroupBy(x => new { x.Date.Year, x.Date.Month })
                .Select(g => new InvestmentDiaryPeriodSummary
                {
                    PeriodDate = new DateTime(g.Key.Year, g.Key.Month, 1),
                    PeriodLabel = new DateTime(g.Key.Year, g.Key.Month, 1).ToString("MMM yyyy"),
                    TotalCost = g.Sum(x => x.TotalCost),
                    CurrentValue = g.Sum(x => x.CurrentValue),
                    Dividend = g.Sum(x => x.Dividend),
                    DividendReinvestment = g.Sum(x => x.DividendReinvestment),
                    Rows = g.OrderBy(x => x.Date).ToList()
                })
                .OrderByDescending(x => x.PeriodDate)
                .ToList();

            return View(new InvestmentDiaryResponseModel
            {
                BonusSplitAdjusted = bonusSplitAdjusted,
                Rows = rows,
                Yearly = yearly,
                Monthly = monthly,
                TotalCost = rows.Sum(x => x.TotalCost),
                CurrentValue = rows.Sum(x => x.CurrentValue),
                Dividend = rows.Sum(x => x.Dividend),
                DividendReinvestment = rows.Sum(x => x.DividendReinvestment)
            });
        }

        private static void ApplyBonusSplitAdjustments(List<InvestmentDiaryRow> list)
        {
            static bool IsTag(InvestmentDiaryRow row, string tag) =>
                string.Equals((row.Tag ?? "").Trim(), tag, StringComparison.OrdinalIgnoreCase);

            var bonusSplitItems = list
                .Where(x => IsTag(x, "Bonus") || IsTag(x, "Split"))
                .OrderBy(x => x.Date)
                .ToList();

            foreach (var bonusItem in bonusSplitItems)
            {
                var previousItems = list.Where(x =>
                        string.Equals(x.Symbol, bonusItem.Symbol, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(x.Portfolio, bonusItem.Portfolio, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(x.Account, bonusItem.Account, StringComparison.OrdinalIgnoreCase) &&
                        x.Date < bonusItem.Date &&
                        !IsTag(x, "Bonus") &&
                        !IsTag(x, "Split"))
                    .ToList();

                if (previousItems.Count == 0)
                {
                    continue;
                }

                var previousTotalQty = previousItems.Sum(x => x.Quantity);
                var totalQuantityAfter = previousTotalQty + bonusItem.Quantity;
                var totalCostBefore = previousItems.Sum(x => x.TotalCost);

                if (previousTotalQty <= 0 || totalQuantityAfter <= 0)
                {
                    continue;
                }

                foreach (var prev in previousItems)
                {
                    var oldQty = prev.Quantity;
                    var newBuyPrice = totalCostBefore / totalQuantityAfter;
                    prev.BuyPrice = newBuyPrice;
                    prev.Quantity = prev.Quantity + (bonusItem.Quantity * prev.Quantity / previousTotalQty);
                    prev.TotalCost = prev.Quantity * newBuyPrice;
                    prev.CurrentValue = oldQty > 0 ? (prev.CurrentValue / oldQty) * prev.Quantity : prev.CurrentValue;
                }
            }
        }

        private static List<InvestmentDiaryRow> ParseRows(List<List<object>> rows)
        {
            static double ParseDouble(object? value)
            {
                var text = value?.ToString()?.Trim();
                if (string.IsNullOrWhiteSpace(text))
                {
                    return 0;
                }

                text = text.Replace(",", "").Replace("₹", "").Replace("$", "");
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

            var list = new List<InvestmentDiaryRow>();
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

                list.Add(new InvestmentDiaryRow
                {
                    Symbol = symbol,
                    Name = name,
                    Account = row[2]?.ToString()?.Trim() ?? "",
                    Date = ParseDate(row[3]),
                    Quantity = ParseDouble(row[4]),
                    BuyPrice = ParseDouble(row[5]),
                    TotalCost = ParseDouble(row[6]),
                    CurrentValue = ParseDouble(row[7]),
                    Dividend = ParseDouble(row[9]),
                    DividendReinvestment = ParseDouble(row[10]),
                    Portfolio = row[11]?.ToString()?.Trim() ?? "",
                    Tag = row[12]?.ToString()?.Trim() ?? "",
                    Ageing = row[13]?.ToString()?.Trim() ?? "",
                    Comments = row[14]?.ToString()?.Trim() ?? "",
                    Change = ParseDouble(row[15])
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
