using System.Collections.Generic;

namespace PortfolioNew.Models
{
    public class PortfolioDiaryMatrixRow
    {
        public string PeriodLabel { get; set; } = "";
        public Dictionary<string, double> PortfolioValues { get; set; } = new();
        public double Total { get; set; }
    }

    public class PortfolioDiaryResponseModel
    {
        public List<string> Portfolios { get; set; } = new();
        public List<PortfolioDiaryMatrixRow> YearlyRows { get; set; } = new();
        public List<PortfolioDiaryMatrixRow> MonthlyRows { get; set; } = new();
        public double GrandTotal { get; set; }
    }
}
