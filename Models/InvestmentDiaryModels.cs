using System;
using System.Collections.Generic;

namespace PortfolioNew.Models
{
    public class InvestmentDiaryRow
    {
        public string Symbol { get; set; } = "";
        public string Name { get; set; } = "";
        public string Account { get; set; } = "";
        public DateTime Date { get; set; }
        public double Quantity { get; set; }
        public double BuyPrice { get; set; }
        public double TotalCost { get; set; }
        public double CurrentValue { get; set; }
        public double Dividend { get; set; }
        public double DividendReinvestment { get; set; }
        public string Portfolio { get; set; } = "";
        public string Tag { get; set; } = "";
        public string Ageing { get; set; } = "";
        public string Comments { get; set; } = "";
        public double Change { get; set; }
        public double ProfitLoss => CurrentValue - TotalCost;
    }

    public class InvestmentDiaryPeriodSummary
    {
        public DateTime PeriodDate { get; set; }
        public string PeriodLabel { get; set; } = "";
        public double TotalCost { get; set; }
        public double CurrentValue { get; set; }
        public double Dividend { get; set; }
        public double DividendReinvestment { get; set; }
        public List<InvestmentDiaryRow> Rows { get; set; } = new();
        public double ProfitLoss => CurrentValue - TotalCost;
        public double ProfitLossPercent => TotalCost > 0 ? (ProfitLoss * 100 / TotalCost) : 0;
    }

    public class InvestmentDiaryResponseModel
    {
        public bool BonusSplitAdjusted { get; set; }
        public List<InvestmentDiaryRow> Rows { get; set; } = new();
        public List<InvestmentDiaryPeriodSummary> Yearly { get; set; } = new();
        public List<InvestmentDiaryPeriodSummary> Monthly { get; set; } = new();
        public double TotalCost { get; set; }
        public double CurrentValue { get; set; }
        public double Dividend { get; set; }
        public double DividendReinvestment { get; set; }
        public double TotalPnL => CurrentValue - TotalCost;
    }
}
