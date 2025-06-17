using System.ComponentModel.DataAnnotations;

namespace YourTurn.Web.Models
{
    public class AdminLog
    {
        public int Id { get; set; }
        
        public int AdminId { get; set; }
        
        [Required]
        [StringLength(100)]
        public string Action { get; set; } = string.Empty;
        
        public string? Details { get; set; }
        
        [StringLength(45)]
        public string? IpAddress { get; set; }
        
        [StringLength(500)]
        public string? UserAgent { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        // Navigation property
        public virtual Admin Admin { get; set; } = null!;
    }
} 