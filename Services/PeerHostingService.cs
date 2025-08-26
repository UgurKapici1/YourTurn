using Microsoft.AspNetCore.SignalR;
using YourTurn.Web.Hubs;
using YourTurn.Web.Models;
using YourTurn.Web.Interfaces;

namespace YourTurn.Web.Services
{
    // Eşler arası barındırma (peer hosting) durumunu periyodik olarak kontrol eden arka plan servisi
    public class PeerHostingService : BackgroundService
    {
        private readonly ILogger<PeerHostingService> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly TimeSpan _checkInterval = TimeSpan.FromSeconds(30);

        // Gerekli servisleri enjekte eder
        public PeerHostingService(ILogger<PeerHostingService> logger, IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        // Arka plan servisinin ana çalışma döngüsü
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Peer Hosting Service started");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CheckPeerHostsAsync();
                    await Task.Delay(_checkInterval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in Peer Hosting Service");
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }
            }

            _logger.LogInformation("Peer Hosting Service stopped");
        }

        // Eş ana bilgisayarları kontrol eder
        private async Task CheckPeerHostsAsync()
        {
            using var scope = _serviceProvider.CreateScope();
            var gameService = scope.ServiceProvider.GetRequiredService<IGameService>();
            var lobbyStore = scope.ServiceProvider.GetRequiredService<ILobbyStore>();

            var peerHostedLobbies = lobbyStore.GetActiveLobbies().Where(l => l.IsPeerHosted).ToList();

            foreach (var lobby in peerHostedLobbies)
            {
                try
                {
                    // Ana bilgisayarın hala çevrimiçi olup olmadığını kontrol et
                    var isOnline = await gameService.IsPeerHostOnlineAsync(lobby);
                    
                    if (!isOnline && lobby.IsHostOnline)
                    {
                        _logger.LogWarning($"Peer host went offline for lobby {lobby.LobbyCode}");
                        lobby.IsHostOnline = false;

                        // Ana bilgisayar çevrimdışı, ancak artık ana bilgisayar devri yapmıyoruz
                        // Sadece lobiyi çevrimdışı bir ana bilgisayara sahip olarak işaretle
                        await NotifyHostOfflineAsync(lobby.LobbyCode);
                    }
                    else if (isOnline && !lobby.IsHostOnline)
                    {
                        _logger.LogInformation($"Peer host came back online for lobby {lobby.LobbyCode}");
                        lobby.IsHostOnline = true;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error checking peer host for lobby {lobby.LobbyCode}");
                }
            }
        }

        // Ana bilgisayarın çevrimdışı olduğunu istemcilere bildirir
        private async Task NotifyHostOfflineAsync(string lobbyCode)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<LobbyHub>>();
                
                await hubContext.Clients.Group(lobbyCode).SendAsync("HostOffline");
                await hubContext.Clients.Group(lobbyCode).SendAsync("UpdateLobby");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error notifying host offline for lobby {lobbyCode}");
            }
        }
    }
} 