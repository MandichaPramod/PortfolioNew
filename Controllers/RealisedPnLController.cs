using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using PortfolioNew.Models;
using PortfolioNew.Services;
using System.Globalization;

namespace PortfolioNew.Controllers
{
    public class RealisedPnLController : Controller
    {
        private const string InActiveCacheKey = "sheet:InActive:A3:Y";
        private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(15);
        private readonly IMemoryCache _memoryCache;

        public RealisedPnLController(IMemoryCache memoryCache)
        {
            _memoryCache = memoryCache;
        }

        public IActionResult Index()
        {
            var rows = ParseRealisedPnLRows(GetSheetDataCached(InActiveCacheKey, "InActive", "A3:Y"))
                .OrderByDescending(x => x.SellDate)
                .ToList();

            var sections = new List<RealisedPnLSection>
            {
                BuildSection("combined", "Combined", rows),
                BuildSection("delivery", "Delivery", rows.Where(x => string.Equals(x.Type, "Delivery", StringComparison.OrdinalIgnoreCase)).ToList()),
                BuildSection("intraday", "Intraday", rows.Where(x => string.Equals(x.Type, "Intraday", StringComparison.OrdinalIgnoreCase)).ToList())
            };

            return View(new RealisedPnLResponseModel
            {
                Sections = sections
            });
        }

        private RealisedPnLSection BuildSection(string key, string label, List<RealisedPnLRow> rows)
        {
            return new RealisedPnLSection
            {
                Key = key,
                Label = label,
                Trades = rows.OrderByDescending(x => x.SellDate).ToList(),
                Stocks = AggregateBySymbol(rows),
                Yearly = AggregateByPeriod(rows, byMonth: false),
                Monthly = AggregateByPeriod(rows, byMonth: true),
                Summary = BuildSummary(rows)
            };
        }

        private static RealisedPnLSummary BuildSummary(IEnumerable<RealisedPnLRow> rows)
        {
            var list = rows.ToList();
            return new RealisedPnLSummary
            {
                TotalCost = list.Sum(x => x.TotalCost),
                SellValue = list.Sum(x => x.SellValue),
                PnL = list.Sum(x => x.PnL),
                PnLAfterCharges = list.Sum(x => x.PnLAfterCharges),
                TotalCharges = list.Sum(x => x.TotalCharges)
            };
        }

        private static List<RealisedPnLRow> AggregateBySymbol(List<RealisedPnLRow> rows)
        {
            return rows
                .GroupBy(r => r.Symbol, StringComparer.OrdinalIgnoreCase)
                .Select(g =>
                {
                    var first = g.First();
                    var qty = g.Sum(x => x.Quantity);
                    var totalCost = g.Sum(x => x.TotalCost);
                    var sellValue = g.Sum(x => x.SellValue);
                    return new RealisedPnLRow
                    {
                        Symbol = first.Symbol,
                        Name = first.Name,
                        Quantity = qty,
                        BuyPrice = qty > 0 ? totalCost / qty : 0,
                        SellPrice = qty > 0 ? sellValue / qty : 0,
                        Dividend = g.Sum(x => x.Dividend),
                        TotalCost = totalCost,
                        SellValue = sellValue,
                        PnL = g.Sum(x => x.PnL),
                        PnLAfterCharges = g.Sum(x => x.PnLAfterCharges),
                        TotalCharges = g.Sum(x => x.TotalCharges),
                        Brokerage = g.Sum(x => x.Brokerage),
                        STT = g.Sum(x => x.STT),
                        TranCharges = g.Sum(x => x.TranCharges),
                        GST = g.Sum(x => x.GST),
                        SEBICharges = g.Sum(x => x.SEBICharges),
                        StampDuty = g.Sum(x => x.StampDuty),
                        OtherCharges = g.Sum(x => x.OtherCharges),
                        Type = first.Type,
                        SellDate = g.Max(x => x.SellDate)
                    };
                })
                .OrderBy(x => x.Name)
                .ToList();
        }

        private static List<RealisedPnLRow> AggregateByPeriod(List<RealisedPnLRow> rows, bool byMonth)
        {
            var grouped = byMonth
                ? rows.GroupBy(x => new { x.SellDate.Year, x.SellDate.Month })
                : rows.GroupBy(x => new { x.SellDate.Year, Month = 1 });

            return grouped
                .Select(g => new RealisedPnLRow
                {
                    SellDate = byMonth
                        ? new DateTime(g.Key.Year, g.Key.Month, 1)
                        : new DateTime(g.Key.Year, 1, 1),
                    Dividend = g.Sum(x => x.Dividend),
                    TotalCost = g.Sum(x => x.TotalCost),
                    SellValue = g.Sum(x => x.SellValue),
                    PnL = g.Sum(x => x.PnL),
                    PnLAfterCharges = g.Sum(x => x.PnLAfterCharges),
                    TotalCharges = g.Sum(x => x.TotalCharges),
                    Brokerage = g.Sum(x => x.Brokerage),
                    STT = g.Sum(x => x.STT),
                    TranCharges = g.Sum(x => x.TranCharges),
                    GST = g.Sum(x => x.GST),
                    SEBICharges = g.Sum(x => x.SEBICharges),
                    StampDuty = g.Sum(x => x.StampDuty),
                    OtherCharges = g.Sum(x => x.OtherCharges)
                })
                .OrderByDescending(x => x.SellDate)
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

        private static List<RealisedPnLRow> ParseRealisedPnLRows(List<List<object>> rows)
        {
            static double ParseDouble(object? value)
            {
                var text = value?.ToString()?.Trim();
                if (string.IsNullOrWhiteSpace(text))
                {
                    return 0;
                }

                text = text.Replace(",", "").Replace("₹", "").Replace("$", "").Replace("%", "");
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
                if (DateTime.TryParse(text, out var d))
                {
                    return d;
                }
                return DateTime.MinValue;
            }

            var result = new List<RealisedPnLRow>();
            foreach (var row in rows)
            {
                while (row.Count < 25)
                {
                    row.Add("");
                }

                if (row.Count < 25)
                {
                    continue;
                }

                var account = row[0]?.ToString()?.Trim() ?? "";
                var symbol = row[1]?.ToString()?.Trim() ?? "";
                if (string.IsNullOrWhiteSpace(symbol) || string.Equals(symbol, "SYMBOL", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                result.Add(new RealisedPnLRow
                {
                    Account = account,
                    Symbol = symbol,
                    Name = row[2]?.ToString()?.Trim() ?? "",
                    SellDate = ParseDate(row[3]),
                    Quantity = ParseDouble(row[4]),
                    BuyPrice = ParseDouble(row[5]),
                    SellPrice = ParseDouble(row[6]),
                    Dividend = ParseDouble(row[7]),
                    TotalCost = ParseDouble(row[8]),
                    SellValue = ParseDouble(row[9]),
                    PnL = ParseDouble(row[10]),
                    PnLAfterCharges = ParseDouble(row[12]),
                    TotalCharges = ParseDouble(row[14]),
                    Brokerage = ParseDouble(row[16]),
                    STT = ParseDouble(row[17]),
                    TranCharges = ParseDouble(row[18]),
                    GST = ParseDouble(row[19]) + ParseDouble(row[20]),
                    SEBICharges = ParseDouble(row[21]),
                    StampDuty = ParseDouble(row[22]),
                    OtherCharges = ParseDouble(row[23]),
                    Type = row[24]?.ToString()?.Trim() ?? ""
                });
            }

            return result;
        }
    }
}
