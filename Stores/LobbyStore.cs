using YourTurn.Web.Models;

namespace YourTurn.Web.Stores
{
    public static class LobbyStore
    {
        public static List<Lobby> ActiveLobbies { get; set; } = new List<Lobby>();
        
        // Connection tracking for better lobby management
        public static Dictionary<string, string> ConnectionToPlayer { get; set; } = new Dictionary<string, string>();
        public static Dictionary<string, List<string>> PlayerToConnections { get; set; } = new Dictionary<string, List<string>>();
        
        /// <summary>
        /// Registers a connection with a player name
        /// </summary>
        public static void RegisterConnection(string connectionId, string playerName)
        {
            // Remove any existing connection for this player to avoid duplicates
            UnregisterConnection(connectionId);
            
            ConnectionToPlayer[connectionId] = playerName;
            
            if (!PlayerToConnections.ContainsKey(playerName))
            {
                PlayerToConnections[playerName] = new List<string>();
            }
            
            if (!PlayerToConnections[playerName].Contains(connectionId))
            {
                PlayerToConnections[playerName].Add(connectionId);
            }
        }
        
        /// <summary>
        /// Unregisters a connection
        /// </summary>
        public static void UnregisterConnection(string connectionId)
        {
            if (ConnectionToPlayer.TryGetValue(connectionId, out var playerName))
            {
                ConnectionToPlayer.Remove(connectionId);
                
                if (PlayerToConnections.ContainsKey(playerName))
                {
                    PlayerToConnections[playerName].Remove(connectionId);
                    
                    // If no more connections for this player, remove the player entry
                    if (PlayerToConnections[playerName].Count == 0)
                    {
                        PlayerToConnections.Remove(playerName);
                    }
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
            return PlayerToConnections.ContainsKey(playerName) && PlayerToConnections[playerName].Count > 0;
        }
    }
}
