using YourTurn.Web.Models;
using System.Collections.Concurrent;

namespace YourTurn.Web.Stores
{
    public static class LobbyStore
    {
        private static readonly object _lobbyLock = new object();
        public static List<Lobby> ActiveLobbies { get; set; } = new List<Lobby>();
        
        // Connection tracking for better lobby management - using ConcurrentDictionary for thread safety
        public static ConcurrentDictionary<string, string> ConnectionToPlayer { get; set; } = new ConcurrentDictionary<string, string>();
        public static ConcurrentDictionary<string, List<string>> PlayerToConnections { get; set; } = new ConcurrentDictionary<string, List<string>>();
        
        /// <summary>
        /// Thread-safe way to add a lobby
        /// </summary>
        public static void AddLobby(Lobby lobby)
        {
            lock (_lobbyLock)
            {
                ActiveLobbies.Add(lobby);
            }
        }
        
        /// <summary>
        /// Thread-safe way to remove a lobby
        /// </summary>
        public static void RemoveLobby(Lobby lobby)
        {
            lock (_lobbyLock)
            {
                ActiveLobbies.Remove(lobby);
            }
        }
        
        /// <summary>
        /// Thread-safe way to get all active lobbies
        /// </summary>
        public static List<Lobby> GetActiveLobbies()
        {
            lock (_lobbyLock)
            {
                return new List<Lobby>(ActiveLobbies);
            }
        }
        
        /// <summary>
        /// Thread-safe way to find a lobby by code
        /// </summary>
        public static Lobby? FindLobbyByCode(string code)
        {
            lock (_lobbyLock)
            {
                return ActiveLobbies.FirstOrDefault(l => l.LobbyCode == code);
            }
        }
        
        /// <summary>
        /// Registers a connection with a player name
        /// </summary>
        public static void RegisterConnection(string connectionId, string playerName)
        {
            // Remove any existing connection for this player to avoid duplicates
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
        
        /// <summary>
        /// Unregisters a connection
        /// </summary>
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
                
                // If no more connections for this player, remove the player entry
                if (PlayerToConnections.TryGetValue(playerName, out var connections) && connections.Count == 0)
                {
                    PlayerToConnections.TryRemove(playerName, out _);
                }
            }
        }
        
        /// <summary>
        /// Gets player name from connection ID
        /// </summary>
        public static string? GetPlayerNameFromConnection(string connectionId)
        {
            ConnectionToPlayer.TryGetValue(connectionId, out var playerName);
            return playerName;
        }
        
        /// <summary>
        /// Gets all connection IDs for a player
        /// </summary>
        public static List<string> GetConnectionsFromPlayer(string playerName)
        {
            PlayerToConnections.TryGetValue(playerName, out var connections);
            return connections ?? new List<string>();
        }
        
        /// <summary>
        /// Checks if a player has any active connections
        /// </summary>
        public static bool HasActiveConnections(string playerName)
        {
            return PlayerToConnections.TryGetValue(playerName, out var connections) && connections.Count > 0;
        }
    }
}
