using Microsoft.AspNetCore.SignalR;
using YourTurn.Web.Models;
using YourTurn.Web.Stores;
using YourTurn.Web.Interfaces;
using YourTurn.Web.Services;
using YourTurn.Web.Interfaces;

namespace YourTurn.Web.Hubs
{
    // Lobi ile ilgili gerçek zamanlı iletişimi yönetir
    public class LobbyHub : Hub
    {
        private readonly IGameService _gameService;
        private readonly ILobbyStore _lobbyStore;

        public LobbyHub(IGameService gameService, ILobbyStore lobbyStore)
        {
            _gameService = gameService;
            _lobbyStore = lobbyStore;
        }

        // Bir istemciyi belirli bir lobi grubuna ekler
        public async Task AddToGroup(string lobbyCode)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, lobbyCode);
        }

        // Bir oyuncu bağlantısını kaydeder
        public async Task RegisterPlayer(string playerName)
        {
            _lobbyStore.RegisterConnection(Context.ConnectionId, playerName);
            await Clients.Caller.SendAsync("PlayerRegistered", playerName);
        }

        // Bir lobi için bir eş ana bilgisayar kaydeder
        public async Task RegisterPeerHost(string lobbyCode, string hostIP, int hostPort)
        {
            var lobby = _gameService.FindLobby(lobbyCode);
            if (lobby != null)
            {
                lobby.HostConnectionId = Context.ConnectionId;
                lobby.HostIPAddress = hostIP;
                lobby.HostPort = hostPort;
                lobby.IsPeerHosted = true;
                lobby.LastHostHeartbeat = DateTime.Now;
                lobby.IsHostOnline = true;

                await Clients.Group(lobbyCode).SendAsync("PeerHostRegistered", hostIP, hostPort);
            }
        }

        // Bağlantıyı canlı tutmak için eş ana bilgisayardan sinyal gönderir
        public async Task HostHeartbeat(string lobbyCode)
        {
            var lobby = _gameService.FindLobby(lobbyCode);
            if (lobby != null && lobby.IsPeerHosted)
            {
                lobby.LastHostHeartbeat = DateTime.Now;
                lobby.IsHostOnline = true;
            }
        }

        // Eş ana bilgisayar çevrimdışı olduğunda bildirir
        public async Task HostOffline(string lobbyCode)
        {
            var lobby = _gameService.FindLobby(lobbyCode);
            if (lobby != null && lobby.IsPeerHosted)
            {
                lobby.IsHostOnline = false;
                await Clients.Group(lobbyCode).SendAsync("PeerHostOffline");
            }
        }

        // İstemci bağlantısı kesildiğinde çalışır
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            // Bağlantıdan oyuncu adını al
            var playerName = _lobbyStore.GetPlayerNameFromConnection(Context.ConnectionId);
            
            // Hata ayıklama günlüğü
            Console.WriteLine($"Client disconnected: {Context.ConnectionId}, Player: {playerName}");
            
            // Bağlantısı kesilen istemcinin bir eş ana bilgisayar olup olmadığını kontrol et
            var peerHostLobby = _lobbyStore.FindByHostConnectionId(Context.ConnectionId);
            if (peerHostLobby != null && peerHostLobby.IsPeerHosted)
            {
                Console.WriteLine($"Peer host disconnected from lobby: {peerHostLobby.LobbyCode}");
                peerHostLobby.IsHostOnline = false;
                await Clients.Group(peerHostLobby.LobbyCode).SendAsync("PeerHostOffline");
            }

            // Not: Bağlantılar kesildiğinde lobileri otomatik olarak kapatmıyoruz
            // Lobiler yalnızca ana bilgisayar Ayrıl eylemiyle açıkça ayrıldığında kapatılacaktır
            // Bu, sayfa yenilemeleri veya bağlantı sorunları nedeniyle yanlış lobi kapanmalarını önler

            // Bağlantı kaydını sil
            _lobbyStore.UnregisterConnection(Context.ConnectionId);

            await base.OnDisconnectedAsync(exception);
        }
    }
}
