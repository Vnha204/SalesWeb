using System.Collections.Generic;

namespace SalesWeb.Models
{
    public class ReportData
    {
        public List<string> Labels { get; set; } = new();
        public List<decimal> Revenue { get; set; } = new();
        public decimal TotalRevenue { get; set; }
        public decimal TotalProfit { get; set; }
        public decimal TotalCost { get; set; }

        public List<object> Employees { get; set; } = new();
        public List<decimal> Profit { get; set; } = new();
        public List<decimal> Cost { get; set; } = new();

    }
}
