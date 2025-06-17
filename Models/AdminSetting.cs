using System.ComponentModel.DataAnnotations;

namespace YourTurn.Web.Models
{
    public class AdminSetting
    {
        public int Id { get; set; }
        
        [Required]
        [StringLength(50)]
        public string SettingKey { get; set; } = string.Empty;
        
        public string? SettingValue { get; set; }
        
        [StringLength(200)]
        public string? Description { get; set; }
        
        public int? UpdatedBy { get; set; }
        
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        
        // Navigation property
        public virtual Admin? UpdatedByAdmin { get; set; }
    }
} 