using System.ComponentModel.DataAnnotations;

namespace YourTurn.Web.Models
{
    // Yönetici eylemlerini günlüğe kaydetmek için model
    public class AdminLog
    {
        // Günlük ID'si
        public int Id { get; set; }
        
        // Eylemi gerçekleştiren yönetici ID'si
        public int AdminId { get; set; }
        
        // Gerçekleştirilen eylem
        [Required]
        [StringLength(100)]
        public string Action { get; set; } = string.Empty;
        
        // Eylemle ilgili detaylar
        public string? Details { get; set; }
        
        // Eylemin yapıldığı IP adresi
        [StringLength(45)]
        public string? IpAddress { get; set; }
        
        // Eylemin yapıldığı tarayıcı bilgisi
        [StringLength(500)]
        public string? UserAgent { get; set; }
        
        // Eylemin oluşturulma tarihi
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        // Gezinme özelliği
        public virtual Admin Admin { get; set; } = null!;
    }
} 