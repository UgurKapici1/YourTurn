using Microsoft.AspNetCore.SignalR;

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
    }
}
