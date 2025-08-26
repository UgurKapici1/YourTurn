using System.Collections.Concurrent;
using YourTurn.Web.Models;

namespace YourTurn.Web.Interfaces
{
    // Lobby durumunu yöneten soyutlama (DIP, SRP)
    public interface ILobbyStore
    {
        List<Lobby> GetActiveLobbies();
        void AddLobby(Lobby lobby);
        void RemoveLobby(Lobby lobby);
        Lobby? FindByHostConnectionId(string connectionId);

        void RegisterConnection(string connectionId, string playerName);
        void UnregisterConnection(string connectionId);
        string? GetPlayerNameFromConnection(string connectionId);
        IEnumerable<string> GetActivePlayers();
    }
}

