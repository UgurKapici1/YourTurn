using YourTurn.Web.Models;

namespace YourTurn.Web.Stores
{
    public static class LobbyStore
    {
        public static List<Lobby> ActiveLobbies { get; set; } = new List<Lobby>();
    }
}
