using System.ComponentModel.DataAnnotations;

namespace YourTurn.Web.Models
{
    public class PlayerStat
    {
        public int Id { get; set; }
        
        [Required]
        [StringLength(100)]
        public string PlayerName { get; set; } = string.Empty;
        
        public int TotalGames { get; set; } = 0;
        
        public int Wins { get; set; } = 0;
        
        public int Losses { get; set; } = 0;
        
        public int TotalScore { get; set; } = 0;
        
        public DateTime? LastSeenAt { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        // Navigation properties
        public virtual ICollection<LobbyHistory> LobbyHistories { get; set; } = new List<LobbyHistory>();
    }
} 