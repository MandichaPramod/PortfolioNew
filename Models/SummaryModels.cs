using System;
using System.Collections.Generic;

namespace PortfolioNew.Models
{
    public class SummaryResponseModel
    {
        public string Range { get; set; } = "all";
        public DateTime? RangeStart { get; set; }
        public DateTime RangeEnd { get; set; }
        public int Holdings { get; set; }
        public double TotalInvested { get; set; }
        public double CurrentValue { get; set; }
        public double Dividends { get; set; }
        public double UnrealisedPnL { get; set; }
        public double DailyChange { get; set; }
        public double RealisedPnL { get; set; }
        public DateTime GeneratedAt { get; set; }
        public DateTime BreakdownCacheLoadedAt { get; set; }
        public DateTime InActiveCacheLoadedAt { get; set; }
        public int CacheAgeMinutes { get; set; }
        public int NegativeDividendCount { get; set; }
        public int ZeroQuantityWithValueCount { get; set; }
        public int StaleHoldingCount { get; set; }
        public List<SummaryBucket> PortfolioSummary { get; set; } = new();
        public List<SummaryBucket> AccountSummary { get; set; } = new();
        public List<SummaryRankItem> TopPortfolioByCurrent { get; set; } = new();
        public List<SummaryRankItem> TopPortfolioByPnL { get; set; } = new();
        public List<SummaryRankItem> TopPortfolioByChange { get; set; } = new();
        public List<SummaryRankItem> TopAccountByCurrent { get; set; } = new();
        public List<SummaryRankItem> TopAccountByPnL { get; set; } = new();
        public List<SummaryRankItem> TopAccountByChange { get; set; } = new();
        public List<SummaryHoldingDetail> HoldingDetails { get; set; } = new();
        public List<string> MarketCapLabels { get; set; } = new();
        public List<double> MarketCapInvestedValues { get; set; } = new();
        public List<double> MarketCapCurrentValues { get; set; } = new();
        public List<double> MarketCapPercentValues { get; set; } = new();
        public List<string> ProfitHistoryLabels { get; set; } = new();
        public List<double> ProfitHistoryValues { get; set; } = new();
    }

    public class SummaryBucket
    {
        public string Name { get; set; } = "";
        public int Holdings { get; set; }
        public double TotalInvested { get; set; }
        public double CurrentValue { get; set; }
        public double Dividends { get; set; }
        public double UnrealisedPnL { get; set; }
        public double DailyChange { get; set; }
        public double RealisedPnL { get; set; }
        public double PnLPercent { get; set; }
        public double ChangePercent { get; set; }
        public double DividendYieldPercent { get; set; }
        public double WeightPercent { get; set; }
        public List<double> ValueTrend { get; set; } = new();
        public List<double> PnLTrend { get; set; } = new();
    }

    public class SummaryRankItem
    {
        public string Name { get; set; } = "";
        public double Value { get; set; }
    }

    public class SummaryHoldingDetail
    {
        public string Portfolio { get; set; } = "";
        public string Account { get; set; } = "";
        public string Symbol { get; set; } = "";
        public string Name { get; set; } = "";
        public string Date { get; set; } = "";
        public double Quantity { get; set; }
        public double BuyPrice { get; set; }
        public double CurrentPrice { get; set; }
        public double TotalCost { get; set; }
        public double CurrentValue { get; set; }
        public double Dividends { get; set; }
        public double UnrealisedPnL { get; set; }
        public double DailyChange { get; set; }
        public double RealisedPnL { get; set; }
        public int DaysSinceBuy { get; set; }
    }
}
