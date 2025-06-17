using Microsoft.AspNetCore.SignalR;
using YourTurn.Web.Models;
using YourTurn.Web.Stores;
using YourTurn.Web.Services;

namespace YourTurn.Web.Hubs
{
    public class LobbyHub : Hub
    {
        /// <summary>
        /// Refreshes the lobby for all clients in a lobby group
        /// </summary>
        public async Task RefreshLobby(string lobbyCode)
        {
            await Clients.Group(lobbyCode).SendAsync("UpdateLobby");
        }

        /// <summary>
        /// Adds a client to a specific lobby group
        /// </summary>
        public async Task AddToGroup(string lobbyCode)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, lobbyCode);
        }

        /// <summary>
        /// Registers a player connection
        /// </summary>
        public async Task RegisterPlayer(string playerName)
        {
            LobbyStore.RegisterConnection(Context.ConnectionId, playerName);
            await Clients.Caller.SendAsync("PlayerRegistered", playerName);
        }

        /// <summary>
        /// Registers a peer host for a lobby
        /// </summary>
        public async Task RegisterPeerHost(string lobbyCode, string hostIP, int hostPort)
        {
            var lobby = GameService.FindLobby(lobbyCode);
            if (lobby != null)
            {
                lobby.HostConnectionId = Context.ConnectionId;
                lobby.HostIPAddress = hostIP;
                lobby.HostPort = hostPort;
                lobby.IsPeerHosted = true;
                lobby.LastHostHeartbeat = DateTime.Now;
                lobby.IsHostOnline = true;

                await Clients.Group(lobbyCode).SendAsync("PeerHostRegistered", hostIP, hostPort);
                await Clients.Group(lobbyCode).SendAsync("UpdateLobby");
            }
        }

        /// <summary>
        /// Sends heartbeat from peer host to keep connection alive
        /// </summary>
        public async Task HostHeartbeat(string lobbyCode)
        {
            var lobby = GameService.FindLobby(lobbyCode);
            if (lobby != null && lobby.IsPeerHosted)
            {
                lobby.LastHostHeartbeat = DateTime.Now;
                lobby.IsHostOnline = true;
            }
        }

        /// <summary>
        /// Notifies when peer host goes offline
        /// </summary>
        public async Task HostOffline(string lobbyCode)
        {
            var lobby = GameService.FindLobby(lobbyCode);
            if (lobby != null && lobby.IsPeerHosted)
            {
                lobby.IsHostOnline = false;
                await Clients.Group(lobbyCode).SendAsync("PeerHostOffline");
                await Clients.Group(lobbyCode).SendAsync("UpdateLobby");
            }
        }

        /// <summary>
        /// Gets peer host information for a lobby
        /// </summary>
        public async Task GetPeerHostInfo(string lobbyCode)
        {
            var lobby = GameService.FindLobby(lobbyCode);
            if (lobby != null && lobby.IsPeerHosted && lobby.IsHostOnline)
            {
                await Clients.Caller.SendAsync("PeerHostInfo", lobby.HostIPAddress, lobby.HostPort);
            }
        }

        /// <summary>
        /// Handles client disconnection
        /// </summary>
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            // Get player name from connection
            var playerName = LobbyStore.GetPlayerNameFromConnection(Context.ConnectionId);
            
            // Debug logging
            Console.WriteLine($"Client disconnected: {Context.ConnectionId}, Player: {playerName}");
            
            // Check if disconnected client was a peer host
            var peerHostLobby = LobbyStore.ActiveLobbies.FirstOrDefault(l => l.HostConnectionId == Context.ConnectionId);
            if (peerHostLobby != null && peerHostLobby.IsPeerHosted)
            {
                Console.WriteLine($"Peer host disconnected from lobby: {peerHostLobby.LobbyCode}");
                peerHostLobby.IsHostOnline = false;
                await Clients.Group(peerHostLobby.LobbyCode).SendAsync("PeerHostOffline");
                await Clients.Group(peerHostLobby.LobbyCode).SendAsync("UpdateLobby");
            }

            // Note: We're not automatically closing lobbies when connections are lost
            // Lobbies will only be closed when the host explicitly leaves via the Leave action
            // This prevents false lobby closures due to page refreshes or connection issues

            // Unregister connection
            LobbyStore.UnregisterConnection(Context.ConnectionId);

            await base.OnDisconnectedAsync(exception);
        }
    }
}
