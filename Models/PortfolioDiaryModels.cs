using System.Collections.Generic;
using System;

namespace PortfolioNew.Models
{
    public class PortfolioDiaryTransactionRow
    {
        public string Symbol { get; set; } = "";
        public string Name { get; set; } = "";
        public string Account { get; set; } = "";
        public string Tag { get; set; } = "";
        public string Portfolio { get; set; } = "";
        public DateTime Date { get; set; }
        public double TotalCost { get; set; }
        public double Quantity { get; set; }
        public double BuyPrice { get; set; }
    }

    public class PortfolioDiaryMatrixRow
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public string PeriodLabel { get; set; } = "";
        public Dictionary<string, double> PortfolioValues { get; set; } = new();
        public double Total { get; set; }
        public string TopPortfolio { get; set; } = "";
        public double TopValue { get; set; }
        public double Delta { get; set; }
        public double DeltaPercent { get; set; }
    }

    public class PortfolioDiaryResponseModel
    {
        public string AccountFilter { get; set; } = "All";
        public string TagFilter { get; set; } = "All";
        public string SymbolFilter { get; set; } = "";
        public List<string> Accounts { get; set; } = new();
        public List<string> Tags { get; set; } = new();
        public List<string> Portfolios { get; set; } = new();
        public List<PortfolioDiaryTransactionRow> Transactions { get; set; } = new();
        public List<PortfolioDiaryMatrixRow> YearlyRows { get; set; } = new();
        public List<PortfolioDiaryMatrixRow> MonthlyRows { get; set; } = new();
        public double GrandTotal { get; set; }
    }
}
