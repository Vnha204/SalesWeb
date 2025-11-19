using System;
using System.ComponentModel.DataAnnotations;

namespace SalesWeb.Models
{
    public class User
    {
        [Key]
        public int user_id { get; set; }

        [Required]
        [StringLength(150)]
        public string full_name { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string username { get; set; } = string.Empty;

        [Required]
        [StringLength(255)]
        public string password_hash { get; set; } = string.Empty;

        [Required]
        [StringLength(20)]
        public string role { get; set; } = string.Empty;

        [StringLength(100)]
        public string? position { get; set; }

        public DateTime created_at { get; set; }
    }
}
