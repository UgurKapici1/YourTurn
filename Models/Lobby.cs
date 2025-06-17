namespace YourTurn.Web.Models
{
    public class Lobby
    {
        public string LobbyCode { get; set; } = Guid.NewGuid().ToString().Substring(0, 6).ToUpper();
        public required string HostPlayerName { get; set; }
        public string? Category { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public List<Player> Players { get; set; } = new();
        public bool IsGameStarted { get; set; } = false;
        public GameState? GameState { get; set; }
        
        // Peer-to-Peer Hosting Properties
        public string? HostConnectionId { get; set; }
        public string? HostIPAddress { get; set; }
        public int? HostPort { get; set; }
        public bool IsPeerHosted { get; set; } = false;
        public DateTime? LastHostHeartbeat { get; set; }
        public bool IsHostOnline { get; set; } = true;
    }
}
