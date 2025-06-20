using Microsoft.AspNetCore.SignalR;

namespace YourTurn.Web.Hubs
{
    // Oyunla ilgili gerçek zamanlı iletişimi yönetir
    public class GameHub : Hub
    {
        // Bir istemciyi belirli bir lobi grubuna ekler
        public async Task AddToGroup(string lobbyCode)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, lobbyCode);
        }

        // Bir istemciyi belirli bir lobi grubundan kaldırır
        public async Task RemoveFromGroup(string lobbyCode)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, lobbyCode);
        }
    }
}