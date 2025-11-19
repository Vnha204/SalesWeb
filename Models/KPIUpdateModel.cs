namespace SalesWeb.Models
{
    public class KPIUpdateModel
    {
        public int UserId { get; set; }
        public int Month { get; set; } = DateTime.Now.Month;
        public int Year { get; set; } = DateTime.Now.Year;
        public decimal TargetSales { get; set; }
        public string Evaluation { get; set; }
    }
}
