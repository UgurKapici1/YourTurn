using System.ComponentModel.DataAnnotations;

namespace YourTurn.Web.Models
{
    public class LobbyHistory
    {
        public int Id { get; set; }
        
        [Required]
        [StringLength(10)]
        public string LobbyCode { get; set; } = string.Empty;
        
        [Required]
        [StringLength(100)]
        public string HostPlayerName { get; set; } = string.Empty;
        
        [StringLength(50)]
        public string? Category { get; set; }
        
        public int PlayerCount { get; set; }
        
        public int? GameDuration { get; set; } // dakika cinsinden
        
        [StringLength(10)]
        public string? Winner { get; set; }
        
        [StringLength(20)]
        public string? FinalScore { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        public DateTime? EndedAt { get; set; }
        
        // Navigation properties
        public virtual ICollection<PlayerStat> PlayerStats { get; set; } = new List<PlayerStat>();
    }
} 