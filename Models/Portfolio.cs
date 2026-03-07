namespace PortfolioNew.Models
{
    public class BreakDownRow
    {
        public string Portfolio { get; set; } = "";
        public string Name { get; set; } = "";
        public string Symbol { get; set; } = "";
        public string Date { get; set; } = "";
        public double Quantity { get; set; }
        public double BuyPrice { get; set; }
        public double CurrentPrice { get; set; }
        public double ProfitLoss { get; set; }
        public double Dividend { get; set; }
        public double DividendReinvested { get; set; }
        public double TotalCost { get; set; }
        public double CurrentValue { get; set; }
        public double Change { get; set; }
        public double RealisedPnL { get; set; }
        public string Tag { get; set; } = "";
        public string Ageing { get; set; } = "";
        public string Comments { get; set; } = "";
        public double PnL => ProfitLoss;
        public string Account { get; set; } = "Default";
    }
}
