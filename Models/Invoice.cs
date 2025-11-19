using System.ComponentModel.DataAnnotations.Schema;

namespace SalesWeb.Models

{
    public class InvoiceRequest
    {
        public int InvoiceId { get; set; }

        public int SellerId { get; set; }
        public List<InvoiceDetail> Details { get; set; }
    }

    public class InvoiceDetail
    {
        public int InvoiceId { get; set; }
        public int ProductId { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
    }

    public class Invoice
    {
        [Column("invoice_id")]
        public int InvoiceId { get; set; }

        [Column("seller_id")]
        public int SellerId { get; set; }

        [Column("invoice_date")]
        public DateTime InvoiceDate { get; set; }

        [Column("total_amount")]
        public decimal TotalAmount { get; set; }

        [Column("status")]
        public string Status { get; set; } = "completed";
        public int? ReturnInvoiceId { get; set; } 


        public List<InvoiceDetail> Details { get; set; } = new();
    }
    public class ReturnRequest
    {
        public int OriginalInvoiceId { get; set; }
        public int ProcessedBy { get; set; }
        public string? Reason { get; set; }
        public List<ReturnDetail> Details { get; set; } = new();
    }

    public class ReturnDetail
    {
        public int ProductId { get; set; }
        public int Quantity { get; set; }
        public decimal RefundPrice { get; set; }
    }
}
