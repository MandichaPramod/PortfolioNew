using System.Collections.Generic;

namespace PortfolioNew.Models
{
    public class PortfolioResponseModel
    {
        public Dictionary<string, List<BreakDownRow>> AllLists { get; set; } = new();
        public List<BreakDownRow> TradeRows { get; set; } = new();
    }
}
