using Microsoft.AspNetCore.SignalR;
using YourTurn.Web.Hubs;
using YourTurn.Web.Models;
using YourTurn.Web.Stores;

namespace YourTurn.Web.Services
{
    public class PeerHostingService : BackgroundService
    {
        private readonly ILogger<PeerHostingService> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly TimeSpan _checkInterval = TimeSpan.FromSeconds(30);

        public PeerHostingService(ILogger<PeerHostingService> logger, IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

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

        private async Task CheckPeerHostsAsync()
        {
            var peerHostedLobbies = LobbyStore.ActiveLobbies.Where(l => l.IsPeerHosted).ToList();

            foreach (var lobby in peerHostedLobbies)
            {
                try
                {
                    // Check if host is still online
                    var isOnline = GameService.IsPeerHostOnline(lobby);
                    
                    if (!isOnline && lobby.IsHostOnline)
                    {
                        _logger.LogWarning($"Peer host went offline for lobby {lobby.LobbyCode}");
                        lobby.IsHostOnline = false;

                        // Host is offline, but we don't transfer host anymore
                        // Just mark the lobby as having an offline host
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