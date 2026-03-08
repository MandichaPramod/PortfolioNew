using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using PortfolioNew.Models;
using PortfolioNew.Services;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace PortfolioNew.Controllers
{
    public class SummaryController : Controller
    {
        private const string BreakdownCacheKey = "sheet:Breakdown:A3:P";
        private const string InActiveCacheKey = "sheet:InActive:A3:Y";
        private const string NetworthOverviewCacheKey = "sheet:Networth OverView:A3:C";
        private static readonly TimeSpan SheetCacheDuration = TimeSpan.FromMinutes(15);
        private readonly IMemoryCache _memoryCache;

        public SummaryController(IMemoryCache memoryCache)
        {
            _memoryCache = memoryCache;
        }

        private sealed class CachedSheetData
        {
            public List<List<object>> Rows { get; init; } = new();
            public DateTime LoadedAt { get; init; }
        }

        private static List<List<object>> CloneSheetData(IEnumerable<IEnumerable<object>>? source)
        {
            if (source == null)
            {
                return new List<List<object>>();
            }

            return source.Select(row => row.Select(cell => cell ?? "").ToList()).ToList();
        }

        private CachedSheetData GetSheetDataCached(string cacheKey, string sheet, string range)
        {
            if (!_memoryCache.TryGetValue(cacheKey, out CachedSheetData? cachedData))
            {
                var fetched = GoogleSheetsService.GetDataFromSheet(sheet, range);
                cachedData = new CachedSheetData
                {
                    Rows = CloneSheetData(fetched),
                    LoadedAt = DateTime.Now
                };
                _memoryCache.Set(cacheKey, cachedData, new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = SheetCacheDuration
                });
            }

            return new CachedSheetData
            {
                Rows = CloneSheetData(cachedData?.Rows),
                LoadedAt = cachedData?.LoadedAt ?? DateTime.Now
            };
        }

        public IActionResult Index(string range = "all")
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

            static DateTime? ParseDate(string? value)
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    return null;
                }

                return DateTime.TryParse(value, out var d) ? d.Date : (DateTime?)null;
            }

            var normalizedRange = (range ?? "all").Trim().ToLowerInvariant();
            if (normalizedRange != "mtd" && normalizedRange != "ytd" && normalizedRange != "1y")
            {
                normalizedRange = "all";
            }

            var now = DateTime.Now;
            DateTime? rangeStart = normalizedRange switch
            {
                "mtd" => new DateTime(now.Year, now.Month, 1),
                "ytd" => new DateTime(now.Year, 1, 1),
                "1y" => now.Date.AddYears(-1),
                _ => null
            };

            var dataCache = GetSheetDataCached(BreakdownCacheKey, "Breakdown", "A3:P");
            var inActiveCache = GetSheetDataCached(InActiveCacheKey, "InActive", "A3:Y");
            var networthCache = GetSheetDataCached(NetworthOverviewCacheKey, "Networth OverView", "A3:C");
            var data = dataCache.Rows;
            var inActiveData = inActiveCache.Rows;
            var networthRows = networthCache.Rows;

            if (data.Count == 0)
            {
                return View(new SummaryResponseModel
                {
                    GeneratedAt = now,
                    Range = normalizedRange,
                    RangeStart = rangeStart,
                    RangeEnd = now,
                    BreakdownCacheLoadedAt = dataCache.LoadedAt,
                    InActiveCacheLoadedAt = inActiveCache.LoadedAt,
                    CacheAgeMinutes = Math.Max(0, (int)Math.Floor((now - dataCache.LoadedAt).TotalMinutes))
                });
            }

            for (int i = 0; i < data.Count; i++)
            {
                while (data[i].Count < 16)
                {
                    data[i].Add("");
                }
            }

            for (int i = 0; i < inActiveData.Count; i++)
            {
                while (inActiveData[i].Count < 25)
                {
                    inActiveData[i].Add("");
                }
            }

            var realisedBySymbol = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            var realisedByAccountSymbol = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

            foreach (var row in inActiveData.Where(r =>
                r.Count >= 25 &&
                !string.IsNullOrWhiteSpace(r[1]?.ToString()) &&
                !string.Equals(r[1]?.ToString()?.Trim(), "SYMBOL", StringComparison.OrdinalIgnoreCase)))
            {
                var account = row[0]?.ToString()?.Trim() ?? "";
                var symbol = row[1]?.ToString()?.Trim() ?? "";
                var pnlAfterCharges = ParseDouble(row[12]);

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

                var key = $"{account}|{symbol}";
                if (realisedByAccountSymbol.ContainsKey(key))
                {
                    realisedByAccountSymbol[key] += pnlAfterCharges;
                }
                else
                {
                    realisedByAccountSymbol[key] = pnlAfterCharges;
                }
            }

            var rows = data
                .Where(row => row.Count >= 16
                    && !string.IsNullOrWhiteSpace(row[0]?.ToString())
                    && !string.IsNullOrWhiteSpace(row[1]?.ToString())
                    && !string.Equals(row[0]?.ToString()?.Trim(), "SYMBOL", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(row[1]?.ToString()?.Trim(), "Name", StringComparison.OrdinalIgnoreCase))
                .Select(row =>
                {
                    var account = row[2]?.ToString() ?? "";
                    var symbol = row[0]?.ToString() ?? "";
                    var accountKey = $"{account}|{symbol}";
                    var realised = realisedByAccountSymbol.TryGetValue(accountKey, out var byAccount)
                        ? byAccount
                        : realisedBySymbol.TryGetValue(symbol, out var bySymbol) ? bySymbol : 0;

                    return new BreakDownRow
                    {
                        Symbol = symbol,
                        Name = row[1]?.ToString() ?? "",
                        Account = account,
                        Date = row[3]?.ToString() ?? "",
                        Quantity = ParseDouble(row[4]),
                        BuyPrice = ParseDouble(row[5]),
                        TotalCost = ParseDouble(row[6]),
                        CurrentValue = ParseDouble(row[7]),
                        ProfitLoss = ParseDouble(row[8]),
                        Dividend = ParseDouble(row[9]) - ParseDouble(row[10]),
                        DividendReinvested = ParseDouble(row[10]),
                        Portfolio = row[11]?.ToString() ?? "",
                        Tag = row[12]?.ToString() ?? "",
                        Ageing = row[13]?.ToString() ?? "",
                        Comments = row[14]?.ToString() ?? "",
                        Change = ParseDouble(row[15]),
                        RealisedPnL = realised
                    };
                })
                .ToList();

            var filteredRows = rows.Where(r =>
            {
                if (rangeStart == null)
                {
                    return true;
                }

                var d = ParseDate(r.Date);
                return d.HasValue && d.Value >= rangeStart.Value;
            }).ToList();

            var grandCurrent = filteredRows.Sum(x => x.CurrentValue);

            static List<double> BuildTrend(IEnumerable<BreakDownRow> items, Func<BreakDownRow, double> selector)
            {
                var grouped = items
                    .Select(x => new { Row = x, Date = ParseDate(x.Date) })
                    .Where(x => x.Date.HasValue)
                    .GroupBy(x => $"{x.Date!.Value.Year:D4}-{x.Date!.Value.Month:D2}")
                    .OrderBy(x => x.Key)
                    .Select(g => g.Sum(x => selector(x.Row)))
                    .ToList();

                if (grouped.Count == 0)
                {
                    return new List<double> { 0, 0 };
                }

                if (grouped.Count == 1)
                {
                    return new List<double> { grouped[0], grouped[0] };
                }

                return grouped;
            }

            SummaryBucket BuildBucket(string name, IEnumerable<BreakDownRow> items)
            {
                var list = items.ToList();
                var invested = list.Sum(x => x.TotalCost);
                var current = list.Sum(x => x.CurrentValue);
                var dividend = list.Sum(x => x.Dividend);
                var pnl = list.Sum(x => x.PnL);
                var change = list.Sum(x => x.Change);
                return new SummaryBucket
                {
                    Name = name,
                    Holdings = list
                        .Where(x => !string.IsNullOrWhiteSpace(x.Symbol))
                        .Select(x => x.Symbol.Trim().ToUpperInvariant())
                        .Distinct()
                        .Count(),
                    TotalInvested = invested,
                    CurrentValue = current,
                    Dividends = dividend,
                    UnrealisedPnL = pnl,
                    DailyChange = change,
                    RealisedPnL = list.Sum(x => x.RealisedPnL),
                    PnLPercent = invested > 0 ? (pnl * 100 / invested) : 0,
                    ChangePercent = (current - change) > 0 ? (100 * change / (current - change)) : 0,
                    DividendYieldPercent = invested > 0 ? (dividend * 100 / invested) : 0,
                    WeightPercent = grandCurrent > 0 ? (current * 100 / grandCurrent) : 0,
                    ValueTrend = BuildTrend(list, x => x.CurrentValue),
                    PnLTrend = BuildTrend(list, x => x.PnL)
                };
            }

            var portfolioSummary = filteredRows
                .Where(x => !string.IsNullOrWhiteSpace(x.Portfolio))
                .GroupBy(x => x.Portfolio.Trim())
                .Select(g => BuildBucket(g.Key, g))
                .OrderByDescending(x => x.CurrentValue)
                .ToList();

            var accountSummary = filteredRows
                .Where(x => !string.IsNullOrWhiteSpace(x.Account))
                .GroupBy(x => x.Account.Trim())
                .Select(g => BuildBucket(g.Key, g))
                .OrderByDescending(x => x.CurrentValue)
                .ToList();

            List<SummaryRankItem> TopBy(IEnumerable<SummaryBucket> source, Func<SummaryBucket, double> metric) =>
                source
                    .OrderByDescending(metric)
                    .Take(5)
                    .Select(x => new SummaryRankItem { Name = x.Name, Value = metric(x) })
                    .ToList();

            var holdingDetails = filteredRows.Select(x =>
            {
                var parsed = ParseDate(x.Date);
                var days = parsed.HasValue ? Math.Max(0, (int)(now.Date - parsed.Value).TotalDays) : 99999;
                return new SummaryHoldingDetail
                {
                    Portfolio = x.Portfolio,
                    Account = x.Account,
                    Symbol = x.Symbol,
                    Name = x.Name,
                    Date = x.Date,
                    Quantity = x.Quantity,
                    BuyPrice = x.BuyPrice,
                    CurrentPrice = x.Quantity > 0 ? (x.CurrentValue / x.Quantity) : 0,
                    TotalCost = x.TotalCost,
                    CurrentValue = x.CurrentValue,
                    Dividends = x.Dividend,
                    UnrealisedPnL = x.PnL,
                    DailyChange = x.Change,
                    RealisedPnL = x.RealisedPnL,
                    DaysSinceBuy = days
                };
            }).ToList();

            static DateTime? ParseHistoryDate(object? value)
            {
                var t = value?.ToString()?.Trim();
                if (string.IsNullOrWhiteSpace(t))
                {
                    return null;
                }

                return DateTime.TryParse(t, out var d) ? d : (DateTime?)null;
            }

            var historyEntries = new List<(DateTime Month, double Invested, double Current, double Percent)>();
            for (int i = 0; i < networthRows.Count; i++)
            {
                var investedRow = networthRows[i];
                if (investedRow.Count < 3)
                {
                    continue;
                }

                var month = ParseHistoryDate(investedRow[0]);
                if (!month.HasValue)
                {
                    continue;
                }

                var invested = ParseDouble(investedRow[2]);
                var current = invested;
                if (i + 1 < networthRows.Count && networthRows[i + 1].Count >= 3)
                {
                    current = ParseDouble(networthRows[i + 1][2]);
                    i++;
                }

                var percent = invested > 0 ? Math.Round(((current - invested) * 100 / invested), 2) : 0;
                historyEntries.Add((month.Value, invested, current, percent));
            }

            historyEntries = historyEntries.OrderBy(x => x.Month).ToList();

            var allTotalInvested = rows.Sum(x => x.TotalCost);
            var allTotalCurrent = rows.Sum(x => x.CurrentValue);
            if (allTotalInvested > 0 || allTotalCurrent > 0)
            {
                var monthKey = new DateTime(now.Year, now.Month, 1);
                var snapshotPercent = allTotalInvested > 0
                    ? Math.Round(((allTotalCurrent - allTotalInvested) * 100 / allTotalInvested), 2)
                    : 0;

                var existingIdx = historyEntries.FindIndex(x => x.Month.Year == monthKey.Year && x.Month.Month == monthKey.Month);
                if (existingIdx >= 0)
                {
                    historyEntries[existingIdx] = (monthKey, allTotalInvested, allTotalCurrent, snapshotPercent);
                }
                else
                {
                    historyEntries.Add((monthKey, allTotalInvested, allTotalCurrent, snapshotPercent));
                    historyEntries = historyEntries.OrderBy(x => x.Month).ToList();
                }
            }

            var marketCapLabels = historyEntries.Select(x => x.Month.ToString("MMM yyyy")).ToList();
            var marketCapInvestedValues = historyEntries.Select(x => x.Invested).ToList();
            var marketCapCurrentValues = historyEntries.Select(x => x.Current).ToList();
            var marketCapPercentValues = historyEntries.Select(x => x.Percent).ToList();
            var profitHistoryLabels = marketCapLabels.ToList();
            var profitHistoryValues = historyEntries.Select(x => x.Current - x.Invested).ToList();

            var model = new SummaryResponseModel
            {
                Range = normalizedRange,
                RangeStart = rangeStart,
                RangeEnd = now,
                Holdings = filteredRows
                    .Where(x => !string.IsNullOrWhiteSpace(x.Symbol))
                    .Select(x => x.Symbol.Trim().ToUpperInvariant())
                    .Distinct()
                    .Count(),
                TotalInvested = filteredRows.Sum(x => x.TotalCost),
                CurrentValue = filteredRows.Sum(x => x.CurrentValue),
                Dividends = filteredRows.Sum(x => x.Dividend),
                UnrealisedPnL = filteredRows.Sum(x => x.PnL),
                DailyChange = filteredRows.Sum(x => x.Change),
                RealisedPnL = filteredRows.Sum(x => x.RealisedPnL),
                GeneratedAt = now,
                BreakdownCacheLoadedAt = dataCache.LoadedAt,
                InActiveCacheLoadedAt = inActiveCache.LoadedAt,
                CacheAgeMinutes = Math.Max(0, (int)Math.Floor((now - dataCache.LoadedAt).TotalMinutes)),
                NegativeDividendCount = filteredRows.Count(x => x.Dividend < 0),
                ZeroQuantityWithValueCount = filteredRows.Count(x => x.Quantity <= 0 && x.CurrentValue > 0),
                StaleHoldingCount = filteredRows.Count(x =>
                {
                    var d = ParseDate(x.Date);
                    return d.HasValue && (now.Date - d.Value).TotalDays > 365;
                }),
                PortfolioSummary = portfolioSummary,
                AccountSummary = accountSummary,
                TopPortfolioByCurrent = TopBy(portfolioSummary, x => x.CurrentValue),
                TopPortfolioByPnL = TopBy(portfolioSummary, x => x.UnrealisedPnL),
                TopPortfolioByChange = TopBy(portfolioSummary, x => x.DailyChange),
                TopAccountByCurrent = TopBy(accountSummary, x => x.CurrentValue),
                TopAccountByPnL = TopBy(accountSummary, x => x.UnrealisedPnL),
                TopAccountByChange = TopBy(accountSummary, x => x.DailyChange),
                HoldingDetails = holdingDetails,
                MarketCapLabels = marketCapLabels,
                MarketCapInvestedValues = marketCapInvestedValues,
                MarketCapCurrentValues = marketCapCurrentValues,
                MarketCapPercentValues = marketCapPercentValues,
                ProfitHistoryLabels = profitHistoryLabels,
                ProfitHistoryValues = profitHistoryValues
            };

            return View(model);
        }
    }
}
