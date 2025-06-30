namespace YourTurn.Web.Models
{
    // Bir oyun lobisini temsil eden model
    public class Lobby
    {
        // Lobinin benzersiz kodu
        public string LobbyCode { get; set; } = Guid.NewGuid().ToString().Substring(0, 6).ToUpper();
        // Lobiyi oluşturan oyuncunun adı
        public required string HostPlayerName { get; set; }
        // Oyun kategorisi
        public string Category { get; set; } = string.Empty;
        // Lobinin oluşturulma tarihi
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        // Lobideki oyuncuların listesi
        public List<Player> Players { get; set; } = new();
        // Oyunun başlayıp başlamadığını belirtir
        public bool IsGameStarted { get; set; } = false;
        // Oyunla ilgili mevcut durumu tutar
        public GameState? GameState { get; set; }
        
        // Eşler arası barındırma özellikleri
        public string? HostConnectionId { get; set; }
        public string? HostIPAddress { get; set; }
        public int? HostPort { get; set; }
        public bool IsPeerHosted { get; set; } = false;
        public DateTime? LastHostHeartbeat { get; set; }
        public bool IsHostOnline { get; set; } = true;
    }
}
