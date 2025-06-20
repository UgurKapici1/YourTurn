using System.ComponentModel.DataAnnotations;

namespace YourTurn.Web.Models
{
    // Oyuncu istatistiklerini tutan model
    public class PlayerStat
    {
        // İstatistik kaydı ID'si
        public int Id { get; set; }
        
        // Oyuncunun adı
        [Required]
        [StringLength(100)]
        public string PlayerName { get; set; } = string.Empty;
        
        // Toplam oynanan oyun sayısı
        public int TotalGames { get; set; } = 0;
        
        // Kazanılan oyun sayısı
        public int Wins { get; set; } = 0;
        
        // Kaybedilen oyun sayısı
        public int Losses { get; set; } = 0;
        
        // Toplam skor
        public int TotalScore { get; set; } = 0;
        
        // Son görülme tarihi
        public DateTime? LastSeenAt { get; set; }
        
        // Kaydın oluşturulma tarihi
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        // Gezinme özellikleri
        public virtual ICollection<LobbyHistory> LobbyHistories { get; set; } = new List<LobbyHistory>();
    }
} 