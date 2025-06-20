using System.ComponentModel.DataAnnotations;

namespace YourTurn.Web.Models
{
    // Tamamlanmış lobilerin geçmişini tutan model
    public class LobbyHistory
    {
        // Geçmiş kaydı ID'si
        public int Id { get; set; }
        
        // Lobinin kodu
        [Required]
        [StringLength(10)]
        public string LobbyCode { get; set; } = string.Empty;
        
        // Lobiyi oluşturan oyuncunun adı
        [Required]
        [StringLength(100)]
        public string HostPlayerName { get; set; } = string.Empty;
        
        // Oyun kategorisi
        [StringLength(50)]
        public string? Category { get; set; }
        
        // Oyuncu sayısı
        public int PlayerCount { get; set; }
        
        // Oyun süresi (dakika cinsinden)
        public int? GameDuration { get; set; }
        
        // Kazanan takım
        [StringLength(10)]
        public string? Winner { get; set; }
        
        // Final skoru
        [StringLength(20)]
        public string? FinalScore { get; set; }
        
        // Lobinin oluşturulma tarihi
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        // Lobinin bitiş tarihi
        public DateTime? EndedAt { get; set; }
        
        // Gezinme özellikleri
        public virtual ICollection<PlayerStat> PlayerStats { get; set; } = new List<PlayerStat>();
    }
} 