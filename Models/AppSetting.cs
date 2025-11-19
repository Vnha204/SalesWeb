using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SalesWeb.Models
{
    public class AppSetting
    {
        [Key]
        [Column("setting_key")]
        public string? SettingKey { get; set; }
        [Column("setting_value")]
        public string? SettingValue { get; set; }
    }
}
