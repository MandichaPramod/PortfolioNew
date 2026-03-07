using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using PortfolioNew.Services;
using PortfolioNew.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace PortfolioNew.Controllers
{
    public class HomeController : Controller
    {
        private const string BreakdownCacheKey = "sheet:Breakdown:A3:P";
        private const string InActiveCacheKey = "sheet:InActive:A3:Y";
        private const string UserSheetCacheKey = "sheet:User:A3:C";
        private static readonly TimeSpan SheetCacheDuration = TimeSpan.FromMinutes(15);
        private readonly IMemoryCache _memoryCache;

        public HomeController(IMemoryCache memoryCache)
        {
            _memoryCache = memoryCache;
        }

        private static List<List<object>> CloneSheetData(IEnumerable<IEnumerable<object>>? source)
        {
            if (source == null)
            {
                return new List<List<object>>();
            }

            return source
                .Select(row => row.Select(cell => cell ?? "").ToList())
                .ToList();
        }

        private List<List<object>> GetSheetDataCached(string cacheKey, string sheet, string range)
        {
            if (!_memoryCache.TryGetValue(cacheKey, out List<List<object>>? cachedData))
            {
                var fetched = GoogleSheetsService.GetDataFromSheet(sheet, range);
                cachedData = CloneSheetData(fetched);
                _memoryCache.Set(cacheKey, cachedData, new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = SheetCacheDuration
                });
            }

            return CloneSheetData(cachedData);
        }

        public IActionResult Index()
        {
            static double ParseDouble(object? value)
            {
                var text = value?.ToString()?.Trim();
                if (string.IsNullOrWhiteSpace(text))
                {
                    return 0;
                }

                text = text.Replace(",", "");
                text = new string(text.Where(c => char.IsDigit(c) || c == '.' || c == '-').ToArray());
                if (string.IsNullOrWhiteSpace(text) || text == "-" || text == ".")
                {
                    return 0;
                }

                return double.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed)
                    ? parsed
                    : 0;
            }

            static BreakDownRow AggregateGroup(IEnumerable<BreakDownRow> groupRows, string portfolioName, string accountName)
            {
                var rows = groupRows.ToList();
                var totalQuantity = rows.Sum(x => x.Quantity);
                var totalCost = rows.Sum(x => x.TotalCost);
                var currentValue = rows.Sum(x => x.CurrentValue);
                var totalDividend = rows.Sum(x => x.Dividend);

                return new BreakDownRow
                {
                    Symbol = rows.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.Symbol))?.Symbol ?? "",
                    Name = rows.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.Name))?.Name ?? "",
                    Account = accountName,
                    Portfolio = portfolioName,
                    Quantity = totalQuantity,
                    TotalCost = totalCost,
                    CurrentValue = currentValue,
                    Dividend = totalDividend,
                    DividendReinvested = rows.Sum(x => x.DividendReinvested),
                    ProfitLoss = rows.Sum(x => x.ProfitLoss),
                    Change = rows.Sum(x => x.Change),
                    RealisedPnL = rows.Sum(x => x.RealisedPnL),
                    BuyPrice = totalQuantity > 0 ? totalCost / totalQuantity : 0,
                    CurrentPrice = totalQuantity > 0 ? currentValue / totalQuantity : 0,
                    Date = rows.OrderByDescending(x => x.Date).FirstOrDefault()?.Date ?? "",
                    Tag = string.Join(", ", rows.Select(x => x.Tag).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct()),
                    Ageing = rows.OrderByDescending(x => x.Date).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.Ageing))?.Ageing ?? "",
                    Comments = rows.OrderByDescending(x => x.Date).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.Comments))?.Comments ?? ""
                };
            }

            var data = GetSheetDataCached(BreakdownCacheKey, "Breakdown", "A3:P");
            var inActiveData = GetSheetDataCached(InActiveCacheKey, "InActive", "A3:Y");
            if (data != null && data.Count > 0)
            {
                // Ensure all rows have at least 16 columns (A:P),
                // because Google Sheets API trims trailing empty cells.
                for (int i = 0; i < data.Count; i++)
                {
                    while (data[i].Count < 16)
                    {
                        data[i].Add("");
                    }
                }

                var rows = data.Where(row => row.Count >= 16 && 
                    !string.IsNullOrWhiteSpace(row[0]?.ToString()) && 
                    !string.IsNullOrWhiteSpace(row[1]?.ToString()) &&
                    !string.Equals(row[0]?.ToString()?.Trim(), "SYMBOL", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(row[1]?.ToString()?.Trim(), "Name", StringComparison.OrdinalIgnoreCase)); // Skip header and empty rows

                // Parse realised PnL from InActive sheet (same source used in reference project).
                var realisedBySymbol = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
                var realisedByAccountSymbol = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
                if (inActiveData != null && inActiveData.Count > 0)
                {
                    for (int i = 0; i < inActiveData.Count; i++)
                    {
                        while (inActiveData[i].Count < 25)
                        {
                            inActiveData[i].Add("");
                        }
                    }

                    foreach (var row in inActiveData.Where(r =>
                        r.Count >= 25 &&
                        !string.IsNullOrWhiteSpace(r[1]?.ToString()) &&
                        !string.Equals(r[1]?.ToString()?.Trim(), "SYMBOL", StringComparison.OrdinalIgnoreCase)))
                    {
                        var account = row[0]?.ToString()?.Trim() ?? "";
                        var symbol = row[1]?.ToString()?.Trim() ?? "";
                        var pnlAfterCharges = ParseDouble(row[12]); // InActive col M in reference parsing

                        if (string.IsNullOrWhiteSpace(symbol))
                        {
                            continue;
                        }

                        if (realisedBySymbol.ContainsKey(symbol))
                        {
                            realisedBySymbol[symbol] += pnlAfterCharges;
                        }
                        else
                        {
                            realisedBySymbol[symbol] = pnlAfterCharges;
                        }

                        var accountSymbolKey = $"{account}|{symbol}";
                        if (realisedByAccountSymbol.ContainsKey(accountSymbolKey))
                        {
                            realisedByAccountSymbol[accountSymbolKey] += pnlAfterCharges;
                        }
                        else
                        {
                            realisedByAccountSymbol[accountSymbolKey] = pnlAfterCharges;
                        }
                    }
                }

                var portfolios = new List<BreakDownRow>();
                foreach (var row in rows)
                {
                    if (row.Count >= 16)
                    {
                        var quantity = ParseDouble(row[4]);
                        var currentValue = ParseDouble(row[7]);

                        portfolios.Add(new BreakDownRow
                        {
                            Symbol = row[0]?.ToString() ?? "",
                            Name = row[1]?.ToString() ?? "",
                            Account = row[2]?.ToString() ?? "",
                            Date = row[3]?.ToString() ?? "",
                            Quantity = quantity,
                            BuyPrice = ParseDouble(row[5]),
                            TotalCost = ParseDouble(row[6]),
                            CurrentValue = currentValue,
                            ProfitLoss = ParseDouble(row[8]),
                            Dividend = ParseDouble(row[9]) - ParseDouble(row[10]),
                            DividendReinvested = ParseDouble(row[10]),
                            Portfolio = row[11]?.ToString() ?? "",
                            Tag = row[12]?.ToString() ?? "",
                            Ageing = row[13]?.ToString() ?? "",
                            Comments = row[14]?.ToString() ?? "",
                            Change = ParseDouble(row[15]),
                            CurrentPrice = quantity > 0 ? currentValue / quantity : 0,
                            RealisedPnL = 0
                        });
                    }
                }
                var allLists = new Dictionary<string, List<BreakDownRow>>();

                // Consolidated: group by symbol across all portfolios/accounts.
                var consolidated = portfolios
                    .GroupBy(p => p.Symbol)
                    .Select(g => AggregateGroup(g, "Consolidated", "All"))
                    .OrderBy(p => p.Name)
                    .ToList();
                foreach (var item in consolidated)
                {
                    item.RealisedPnL = realisedBySymbol.TryGetValue(item.Symbol, out var realisedValue)
                        ? realisedValue
                        : 0;
                }
                allLists["Consolidated"] = consolidated;

                // Portfolio views: group by portfolio -> account -> symbol.
                var portfoliosByPortfolio = portfolios
                    .Where(p => !string.IsNullOrWhiteSpace(p.Portfolio))
                    .GroupBy(p => p.Portfolio);

                foreach (var portfolioGroup in portfoliosByPortfolio)
                {
                    var groupedPortfolioList = portfolioGroup
                        .GroupBy(p => new { p.Account, p.Symbol })
                        .Select(g => AggregateGroup(g, portfolioGroup.Key, g.Key.Account ?? ""))
                        .OrderBy(p => p.Name)
                        .ToList();
                    foreach (var item in groupedPortfolioList)
                    {
                        var accountSymbolKey = $"{item.Account}|{item.Symbol}";
                        if (realisedByAccountSymbol.TryGetValue(accountSymbolKey, out var accountRealised))
                        {
                            item.RealisedPnL = accountRealised;
                        }
                        else if (realisedBySymbol.TryGetValue(item.Symbol, out var symbolRealised))
                        {
                            item.RealisedPnL = symbolRealised;
                        }
                        else
                        {
                            item.RealisedPnL = 0;
                        }
                    }

                    allLists[portfolioGroup.Key] = groupedPortfolioList;
                }

                return View(new PortfolioResponseModel
                {
                    AllLists = allLists,
                    TradeRows = portfolios
                });
            }
            else
            {
                return View(new PortfolioResponseModel());
            }
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ClearCache()
        {
            _memoryCache.Remove(BreakdownCacheKey);
            _memoryCache.Remove(InActiveCacheKey);
            _memoryCache.Remove(UserSheetCacheKey);
            TempData["CacheStatus"] = "Cache cleared. Fresh data will be loaded on next request.";
            return RedirectToAction(nameof(Index));
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View();
        }
    }
}
