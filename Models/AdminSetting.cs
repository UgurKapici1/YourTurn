using System.ComponentModel.DataAnnotations;

namespace YourTurn.Web.Models
{
    // Yönetici ayarlarını temsil eden model
    public class AdminSetting
    {
        // Ayar ID'si
        public int Id { get; set; }
        
        // Ayar anahtarı (benzersiz)
        [Required]
        [StringLength(50)]
        public string SettingKey { get; set; } = string.Empty;
        
        // Ayar değeri
        public string? SettingValue { get; set; }
        
        // Ayarın açıklaması
        [StringLength(200)]
        public string? Description { get; set; }
        
        // Ayarı güncelleyen yönetici ID'si
        public int? UpdatedBy { get; set; }
        
        // Ayarın son güncellenme tarihi
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        
        // Gezinme özelliği
        public virtual Admin? UpdatedByAdmin { get; set; }
    }
} 