using Microsoft.AspNetCore.SignalR;

namespace YourTurn.Web.Hubs
{
    public class GameHub : Hub
    {
        /// <summary>
        /// Adds a client to a specific lobby group
        /// </summary>
        public async Task AddToGroup(string lobbyCode)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, lobbyCode);
        }

        /// <summary>
        /// Removes a client from a specific lobby group
        /// </summary>
        public async Task RemoveFromGroup(string lobbyCode)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, lobbyCode);
        }

        /// <summary>
        /// Sends game update to all clients in a lobby group
        /// </summary>
        public async Task UpdateGame(string lobbyCode)
        {
            await Clients.Group(lobbyCode).SendAsync("UpdateGame");
        }
    }
}