namespace YourTurn.Web.Models
{
    // Bir oyuncuyu temsil eden model
    public class Player
    {
        // Oyuncunun adı
        public required string Name { get; set; }
        // Oyuncunun takımı
        public string Team { get; set; } = "";
    }
}
