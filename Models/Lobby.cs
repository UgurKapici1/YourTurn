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
    }
}
