using YourTurn.Web.Models;
using System.Collections.Concurrent;

namespace YourTurn.Web.Stores
{
    // Aktif lobileri ve oyuncu bağlantılarını bellekte tutan statik sınıf
    public static class LobbyStore
    {
        private static readonly object _lobbyLock = new object();
        public static List<Lobby> ActiveLobbies { get; set; } = new List<Lobby>();
        
        // Daha iyi lobi yönetimi için bağlantı takibi - iş parçacığı güvenliği için ConcurrentDictionary kullanılıyor
        public static ConcurrentDictionary<string, string> ConnectionToPlayer { get; set; } = new ConcurrentDictionary<string, string>();
        public static ConcurrentDictionary<string, List<string>> PlayerToConnections { get; set; } = new ConcurrentDictionary<string, List<string>>();
        
        // İş parçacığı güvenli bir şekilde tüm aktif lobileri alır
        public static List<Lobby> GetActiveLobbies()
        {
            lock (_lobbyLock)
            {
                return new List<Lobby>(ActiveLobbies);
            }
        }
        
        // Bir bağlantıyı bir oyuncu adıyla kaydeder
        public static void RegisterConnection(string connectionId, string playerName)
        {
            // Bu oyuncu için mevcut bağlantıyı kaldırarak kopyaları önle
            UnregisterConnection(connectionId);
            
            ConnectionToPlayer.TryAdd(connectionId, playerName);
            
            PlayerToConnections.AddOrUpdate(
                playerName,
                new List<string> { connectionId },
                (key, existingList) =>
                {
                    if (!existingList.Contains(connectionId))
                    {
                        existingList.Add(connectionId);
                    }
                    return existingList;
                });
        }
        
        // Bir bağlantının kaydını siler
        public static void UnregisterConnection(string connectionId)
        {
            if (ConnectionToPlayer.TryRemove(connectionId, out var playerName))
            {
                PlayerToConnections.AddOrUpdate(
                    playerName,
                    new List<string>(),
                    (key, existingList) =>
                    {
                        existingList.Remove(connectionId);
                        return existingList;
                    });
                
                // Bu oyuncu için başka bağlantı kalmadıysa, oyuncu girişini kaldır
                if (PlayerToConnections.TryGetValue(playerName, out var connections) && connections.Count == 0)
                {
                    PlayerToConnections.TryRemove(playerName, out _);
                }
            }
        }
        
        // Bağlantı ID'sinden oyuncu adını alır
        public static string? GetPlayerNameFromConnection(string connectionId)
        {
            ConnectionToPlayer.TryGetValue(connectionId, out var playerName);
            return playerName;
        }
    }
}
