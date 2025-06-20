using System.ComponentModel.DataAnnotations;

namespace YourTurn.Web.Models
{
    // Yönetici kullanıcıyı temsil eden model
    public class Admin
    {
        // Yönetici ID'si
        public int Id { get; set; }
        
        // Kullanıcı adı
        [Required]
        [StringLength(50)]
        public string Username { get; set; } = string.Empty;
        
        // Şifre hash'i
        [Required]
        [StringLength(255)]
        public string PasswordHash { get; set; } = string.Empty;
        
        // E-posta adresi
        [StringLength(100)]
        public string? Email { get; set; }
        
        // Kullanıcı rolü
        [StringLength(20)]
        public string Role { get; set; } = "Admin";
        
        // Hesabın aktif olup olmadığını belirtir
        public bool IsActive { get; set; } = true;
        
        // Hesabın oluşturulma tarihi
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        // Son giriş tarihi
        public DateTime? LastLoginAt { get; set; }
        
        // Gezinme özellikleri
        public virtual ICollection<AdminLog> AdminLogs { get; set; } = new List<AdminLog>();
        public virtual ICollection<AdminSetting> AdminSettings { get; set; } = new List<AdminSetting>();
    }
} 