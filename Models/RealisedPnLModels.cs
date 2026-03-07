using System;
using System.Collections.Generic;

namespace PortfolioNew.Models
{
    public class RealisedPnLRow
    {
        public string Account { get; set; } = "";
        public string Symbol { get; set; } = "";
        public string Name { get; set; } = "";
        public DateTime SellDate { get; set; }
        public double Quantity { get; set; }
        public double BuyPrice { get; set; }
        public double SellPrice { get; set; }
        public double Dividend { get; set; }
        public double TotalCost { get; set; }
        public double SellValue { get; set; }
        public double PnL { get; set; }
        public double PnLAfterCharges { get; set; }
        public double TotalCharges { get; set; }
        public double Brokerage { get; set; }
        public double STT { get; set; }
        public double TranCharges { get; set; }
        public double GST { get; set; }
        public double SEBICharges { get; set; }
        public double StampDuty { get; set; }
        public double OtherCharges { get; set; }
        public string Type { get; set; } = "";
    }

    public class RealisedPnLSummary
    {
        public double TotalCost { get; set; }
        public double SellValue { get; set; }
        public double PnL { get; set; }
        public double PnLAfterCharges { get; set; }
        public double TotalCharges { get; set; }
        public double PnLPercent => TotalCost > 0 ? (PnL * 100 / TotalCost) : 0;
        public double PnLAfterChargesPercent => TotalCost > 0 ? (PnLAfterCharges * 100 / TotalCost) : 0;
    }

    public class RealisedPnLSection
    {
        public string Key { get; set; } = "";
        public string Label { get; set; } = "";
        public RealisedPnLSummary Summary { get; set; } = new();
        public List<RealisedPnLRow> Trades { get; set; } = new();
        public List<RealisedPnLRow> Stocks { get; set; } = new();
        public List<RealisedPnLRow> Yearly { get; set; } = new();
        public List<RealisedPnLRow> Monthly { get; set; } = new();
    }

    public class RealisedPnLResponseModel
    {
        public List<RealisedPnLSection> Sections { get; set; } = new();
    }
}
