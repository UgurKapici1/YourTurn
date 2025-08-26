using System.Collections.Concurrent;
using YourTurn.Web.Interfaces;
using YourTurn.Web.Models;

namespace YourTurn.Web.Stores
{
    // Thread-safe bellek içi lobby store (eski statik yapı yerine)
    public class InMemoryLobbyStore : ILobbyStore
    {
        private readonly object _lobbyLock = new object();
        private readonly List<Lobby> _activeLobbies = new List<Lobby>();
        private readonly ConcurrentDictionary<string, string> _connectionToPlayer = new ConcurrentDictionary<string, string>();
        private readonly ConcurrentDictionary<string, List<string>> _playerToConnections = new ConcurrentDictionary<string, List<string>>();

        public List<Lobby> GetActiveLobbies()
        {
            lock (_lobbyLock)
            {
                return new List<Lobby>(_activeLobbies);
            }
        }

        public void AddLobby(Lobby lobby)
        {
            lock (_lobbyLock)
            {
                _activeLobbies.Add(lobby);
            }
        }

        public void RemoveLobby(Lobby lobby)
        {
            lock (_lobbyLock)
            {
                _activeLobbies.Remove(lobby);
            }
        }

        public Lobby? FindByHostConnectionId(string connectionId)
        {
            lock (_lobbyLock)
            {
                return _activeLobbies.FirstOrDefault(l => l.HostConnectionId == connectionId);
            }
        }

        public void RegisterConnection(string connectionId, string playerName)
        {
            UnregisterConnection(connectionId);
            _connectionToPlayer.TryAdd(connectionId, playerName);

            _playerToConnections.AddOrUpdate(
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

        public void UnregisterConnection(string connectionId)
        {
            if (_connectionToPlayer.TryRemove(connectionId, out var playerName))
            {
                _playerToConnections.AddOrUpdate(
                    playerName,
                    new List<string>(),
                    (key, existingList) =>
                    {
                        existingList.Remove(connectionId);
                        return existingList;
                    });

                if (_playerToConnections.TryGetValue(playerName, out var connections) && connections.Count == 0)
                {
                    _playerToConnections.TryRemove(playerName, out _);
                }
            }
        }

        public string? GetPlayerNameFromConnection(string connectionId)
        {
            _connectionToPlayer.TryGetValue(connectionId, out var playerName);
            return playerName;
        }

        public IEnumerable<string> GetActivePlayers()
        {
            return _playerToConnections.Keys;
        }
    }
}

