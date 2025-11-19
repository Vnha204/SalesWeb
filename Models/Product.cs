using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace SalesWeb.Models
{
    [Table("Products")]
    public class Product
    {
        [Column("product_id")]
        public int ProductId { get; set; }

        [Column("product_code")]
        public string? ProductCode { get; set; }

        [Column("name")]
        public string Name { get; set; } = null!;

        [Column("price")]
        public decimal Price { get; set; }

        [Column("stock_quantity")]
        public int StockQuantity { get; set; }

        [Column("image_url")]
        public string? ImageUrl { get; set; }

        [Column("description")]
        public string? Description { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        [Column("is_active")]
        public bool IsActive { get; set; } = true;
        [Column("category")]
        public string? Category { get; set; }
    }
}
