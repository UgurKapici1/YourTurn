using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using YourTurn.Web.Hubs;
using YourTurn.Web.Models;
using YourTurn.Web.Services;
using YourTurn.Web.Stores;

namespace YourTurn.Web.Controllers
{
    // Lobi ile ilgili istekleri yönetir
    public class LobbyController : Controller
    {
        private readonly IHubContext<LobbyHub> _hubContext;

        // Gerekli servisleri enjekte eder
        public LobbyController(IHubContext<LobbyHub> hubContext)
        {
            _hubContext = hubContext;
        }

        // Mevcut oyuncuyla yeni bir lobi oluşturur ve ana bilgisayar olarak ayarlar
        [HttpPost]
        public async Task<IActionResult> Create()
        {
            string playerName = GameService.GeneratePlayerName(TempData["PlayerName"]?.ToString());

            var newLobby = new Lobby
            {
                HostPlayerName = playerName
            };

            newLobby.Players.Add(GameService.CreatePlayer(playerName));

            // Automatically start peer hosting
            newLobby.IsPeerHosted = true;
            newLobby.HostIPAddress = "127.0.0.1"; // Default local IP
            newLobby.HostPort = 8080; // Default port
            newLobby.LastHostHeartbeat = DateTime.Now;
            newLobby.IsHostOnline = true;

            LobbyStore.ActiveLobbies.Add(newLobby);
            HttpContext.Session.SetString("PlayerName", playerName);

            // Notify clients about peer hosting
            await _hubContext.Clients.Group(newLobby.LobbyCode).SendAsync("PeerHostRegistered", newLobby.HostIPAddress, newLobby.HostPort);

            return RedirectToAction("LobbyRoom", new { code = newLobby.LobbyCode });
        }

        // Bir oyuncunun mevcut bir lobiye katılmasına izin verir
        [HttpPost]
        public async Task<IActionResult> JoinAsync(string lobbyCode)
        {
            string playerName = GameService.GeneratePlayerName(TempData["PlayerName"]?.ToString());

            var lobby = GameService.FindLobbyIgnoreCase(lobbyCode);
            if (lobby == null)
            {
                ViewBag.Error = "Lobi bulunamadı!";
                return RedirectToAction("JoinLobby", "Home");
            }

            if (!lobby.Players.Any(p => p.Name == playerName))
            {
                lobby.Players.Add(GameService.CreatePlayer(playerName));
            }
            HttpContext.Session.SetString("PlayerName", playerName);
            await _hubContext.Clients.Group(lobbyCode).SendAsync("UpdateLobby");

            return RedirectToAction("LobbyRoom", new { code = lobby.LobbyCode });
        }

        // Belirli bir lobi için lobi odasını görüntüler
        [HttpGet]
        public IActionResult LobbyRoom(string code)
        {
            var lobby = GameService.FindLobby(code);
            if (lobby == null)
                return NotFound("Lobby bulunamadı");

            if (lobby.IsGameStarted)
            {
                return RedirectToAction("Game", "Game", new { code });
            }

            ViewBag.CurrentPlayerName = HttpContext.Session.GetString("PlayerName");

            return View(lobby);
        }

        // Bir lobi için eş ana bilgisayar bilgilerini alır
        [HttpGet]
        public IActionResult GetPeerHostInfo(string code)
        {
            var lobby = GameService.FindLobby(code);
            if (lobby == null)
                return NotFound("Lobby bulunamadı");

            if (!lobby.IsPeerHosted || !lobby.IsHostOnline)
                return NotFound("Peer host bulunamadı");

            return Json(new
            {
                hostIP = lobby.HostIPAddress,
                hostPort = lobby.HostPort,
                isOnline = lobby.IsHostOnline
            });
        }

        // Ana bilgisayarın oyun için bir kategori seçmesine izin verir
        [HttpPost]
        public async Task<IActionResult> ChooseCategory(string code, string category)
        {
            var lobby = GameService.FindLobby(code);
            if (lobby == null || lobby.IsGameStarted)
                return Forbid();

            var currentPlayerName = HttpContext.Session.GetString("PlayerName");
            
            if (lobby.HostPlayerName != currentPlayerName)
                return Forbid();

            lobby.Category = category;

            await _hubContext.Clients.Group(code).SendAsync("UpdateLobby");

            return RedirectToAction("LobbyRoom", new { code });
        }

        // Bir oyuncunun takımını seçmesine veya değiştirmesine izin verir
        [HttpPost]
        public async Task<IActionResult> ChooseTeam(string code, string playerName, string team)
        {
            var lobby = GameService.FindLobby(code);
            if (lobby == null || lobby.IsGameStarted)
                return Forbid();

            var player = lobby.Players.FirstOrDefault(p => p.Name == playerName);
            if (player != null)
            {
                if (lobby.GameState != null)
                {
                    var oldTeam = player.Team;
                    if (oldTeam == "Sol" && lobby.GameState.Team1Volunteer == playerName)
                    {
                        lobby.GameState.Team1Volunteer = null;
                    }
                    else if (oldTeam == "Sağ" && lobby.GameState.Team2Volunteer == playerName)
                    {
                        lobby.GameState.Team2Volunteer = null;
                    }
                }

                player.Team = team;
            }

            await _hubContext.Clients.Group(code).SendAsync("UpdateLobby");

            return RedirectToAction("LobbyRoom", new { code });
        }

        // Bir oyuncunun mevcut takımından ayrılmasına izin verir
        [HttpPost]
        public async Task<IActionResult> LeaveTeam(string code, string playerName)
        {
            var lobby = GameService.FindLobby(code);
            if (lobby == null || lobby.IsGameStarted)
                return NotFound();

            var player = lobby.Players.FirstOrDefault(p => p.Name == playerName);
            if (player != null)
            {
                player.Team = "";
            }

            await _hubContext.Clients.Group(code).SendAsync("UpdateLobby");
            return RedirectToAction("LobbyRoom", new { code });
        }

        // Bir oyuncunun lobiden ayrılmasına izin verir
        [HttpPost]
        public async Task<IActionResult> Leave(string code)
        {
            var lobby = GameService.FindLobby(code);
            if (lobby == null) return RedirectToAction("Index", "Home");

            var currentPlayerName = HttpContext.Session.GetString("PlayerName");
            if (!string.IsNullOrEmpty(currentPlayerName))
            {
                var player = lobby.Players.FirstOrDefault(p => p.Name == currentPlayerName);
                if (player != null)
                {
                    // Check if the leaving player is the host
                    bool isHostLeaving = lobby.HostPlayerName == currentPlayerName;
                    
                    lobby.Players.Remove(player);
                    
                    if (lobby.Players.Count == 0)
                    {
                        // No players left, remove the lobby
                        LobbyStore.ActiveLobbies.Remove(lobby);
                        await _hubContext.Clients.Group(code).SendAsync("LobbyClosed", "Tüm oyuncular ayrıldığı için oda kapatıldı.");
                    }
                    else if (isHostLeaving)
                    {
                        // Host is leaving, close the lobby and notify all players
                        LobbyStore.ActiveLobbies.Remove(lobby);
                        await _hubContext.Clients.Group(code).SendAsync("LobbyClosed", "Host lobiden ayrıldığı için oda kapatıldı.");
                    }
                    else
                    {
                        // Regular player leaving, transfer host if needed
                        if (lobby.HostPlayerName == currentPlayerName && lobby.Players.Count > 0)
                        {
                            lobby.HostPlayerName = lobby.Players.First().Name;
                        }
                        
                        await _hubContext.Clients.Group(code).SendAsync("UpdateLobby");
                    }
                }
            }

            return RedirectToAction("Index", "Home");
        }

        // Tüm koşullar karşılanırsa oyunu başlatır
        [HttpPost]
        public async Task<IActionResult> StartGame(string code)
        {
            var lobby = GameService.FindLobby(code);
            if (lobby == null) return NotFound();

            var (canStart, errorMessage) = GameService.ValidateGameStart(lobby);
            if (!canStart)
            {
                TempData["Error"] = errorMessage;
                return RedirectToAction("LobbyRoom", new { code });
            }

            // Preserve volunteer information and initialize game state
            var team1Volunteer = lobby.GameState.Team1Volunteer;
            var team2Volunteer = lobby.GameState.Team2Volunteer;
            
            lobby.GameState = GameService.InitializeGameState(lobby.Category);
            lobby.GameState.Team1Volunteer = team1Volunteer;
            lobby.GameState.Team2Volunteer = team2Volunteer;
            lobby.GameState.ActivePlayer1 = team1Volunteer;
            lobby.GameState.ActivePlayer2 = team2Volunteer;
            lobby.GameState.CurrentTurn = team1Volunteer;
            lobby.GameState.IsWaitingForVolunteers = false;

            lobby.IsGameStarted = true;

            await _hubContext.Clients.Group(code).SendAsync("GameStarted");

            return RedirectToAction("Game", "Game", new { code });
        }

        // Bir oyuncunun takımı için gönüllü olmasına izin verir
        [HttpPost]
        public async Task<IActionResult> VolunteerForTeam([FromBody] VolunteerRequest request)
        {
            try
            {
                var lobby = GameService.FindLobby(request.code);
                if (lobby == null)
                    return Json(new { success = false, message = "Lobby bulunamadı" });

                var currentPlayerName = HttpContext.Session.GetString("PlayerName");
                var player = lobby.Players.FirstOrDefault(p => p.Name == currentPlayerName);
                
                if (player == null || player.Team != request.team)
                    return Json(new { success = false, message = "Geçersiz oyuncu veya takım" });

                if (lobby.GameState == null)
                {
                    lobby.GameState = new GameState
                    {
                        IsWaitingForVolunteers = true
                    };
                }

                if (request.team == "Sol")
                {
                    lobby.GameState.Team1Volunteer = currentPlayerName;
                }
                else if (request.team == "Sağ")
                {
                    lobby.GameState.Team2Volunteer = currentPlayerName;
                }

                await _hubContext.Clients.Group(request.code).SendAsync("UpdateLobby");

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // Gönüllünün geri çekilmesine izin verir
        [HttpPost]
        public async Task<IActionResult> WithdrawVolunteer([FromBody] VolunteerRequest request)
        {
            try
            {
                var lobby = GameService.FindLobby(request.code);
                if (lobby == null)
                    return Json(new { success = false, message = "Lobby bulunamadı" });

                var currentPlayerName = HttpContext.Session.GetString("PlayerName");
                
                if (lobby.GameState == null || lobby.IsGameStarted)
                    return Json(new { success = false, message = "Bu işlem şu anda yapılamaz" });

                if (request.team == "Sol" && lobby.GameState.Team1Volunteer == currentPlayerName)
                {
                    lobby.GameState.Team1Volunteer = null;
                }
                else if (request.team == "Sağ" && lobby.GameState.Team2Volunteer == currentPlayerName)
                {
                    lobby.GameState.Team2Volunteer = null;
                }
                else
                {
                    return Json(new { success = false, message = "Bu takımda gönüllü değilsiniz" });
                }

                await _hubContext.Clients.Group(request.code).SendAsync("UpdateLobby");

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // Takımları rastgele dağıtır
        [HttpPost]
        public async Task<IActionResult> RandomizeTeams(string code)
        {
            var lobby = GameService.FindLobby(code);
            if (lobby == null)
                return NotFound("Lobby bulunamadı");

            var currentPlayerName = HttpContext.Session.GetString("PlayerName");
            if (lobby.HostPlayerName != currentPlayerName)
                return Forbid("Sadece host takımları rastgele dağıtabilir");

            if (lobby.IsGameStarted)
                return Forbid("Oyun başladıktan sonra takım değişikliği yapılamaz");

            // Get players without teams
            var unassignedPlayers = lobby.Players.Where(p => string.IsNullOrEmpty(p.Team)).ToList();
            
            if (unassignedPlayers.Count < 2)
            {
                TempData["Error"] = "En az 2 oyuncu olması gerekiyor!";
                return RedirectToAction("LobbyRoom", new { code });
            }

            // Randomly assign players to teams
            var random = new Random();
            var shuffledPlayers = unassignedPlayers.OrderBy(x => random.Next()).ToList();
            
            for (int i = 0; i < shuffledPlayers.Count; i++)
            {
                shuffledPlayers[i].Team = (i % 2 == 0) ? "Sol" : "Sağ";
            }

            await _hubContext.Clients.Group(code).SendAsync("UpdateLobby");
            TempData["Success"] = "Oyuncular rastgele takımlara dağıtıldı!";

            return RedirectToAction("LobbyRoom", new { code });
        }

        // Takımları sıfırlar
        [HttpPost]
        public async Task<IActionResult> ResetTeams(string code)
        {
            var lobby = GameService.FindLobby(code);
            if (lobby == null)
                return NotFound("Lobby bulunamadı");

            var currentPlayerName = HttpContext.Session.GetString("PlayerName");
            if (lobby.HostPlayerName != currentPlayerName)
                return Forbid("Sadece host takımları sıfırlayabilir");

            if (lobby.IsGameStarted)
                return Forbid("Oyun başladıktan sonra takım değişikliği yapılamaz");

            // Reset all team assignments
            foreach (var player in lobby.Players)
            {
                player.Team = "";
            }

            await _hubContext.Clients.Group(code).SendAsync("UpdateLobby");
            TempData["Success"] = "Tüm takım atamaları sıfırlandı!";

            return RedirectToAction("LobbyRoom", new { code });
        }

        // Oyuncunun hakem olmasını sağlar
        [HttpPost]
        public async Task<IActionResult> BecomeReferee(string code)
        {
            var lobby = GameService.FindLobby(code);
            if (lobby == null || lobby.IsGameStarted)
                return Forbid();

            var currentPlayerName = HttpContext.Session.GetString("PlayerName");
            // Gönüllüyse gönüllülükten çıkar
            if (lobby.GameState != null)
            {
                if (lobby.GameState.Team1Volunteer == currentPlayerName)
                    lobby.GameState.Team1Volunteer = null;
                if (lobby.GameState.Team2Volunteer == currentPlayerName)
                    lobby.GameState.Team2Volunteer = null;
            }
            if (string.IsNullOrEmpty(lobby.RefereeName))
            {
                lobby.RefereeName = currentPlayerName;
                await _hubContext.Clients.Group(code).SendAsync("UpdateLobby");
                TempData["Success"] = "Artık hakemsiniz!";
            }
            else if (lobby.RefereeName == currentPlayerName)
            {
                TempData["Error"] = "Zaten hakemsiniz.";
            }
            else
            {
                TempData["Error"] = "Bu lobide zaten bir hakem var.";
            }
            return RedirectToAction("LobbyRoom", new { code });
        }

        // Hakemin görevini bırakmasını sağlar
        [HttpPost]
        public async Task<IActionResult> LeaveReferee(string code)
        {
            var lobby = GameService.FindLobby(code);
            if (lobby == null || lobby.IsGameStarted)
                return Forbid();

            var currentPlayerName = HttpContext.Session.GetString("PlayerName");
            if (lobby.RefereeName == currentPlayerName)
            {
                lobby.RefereeName = null;
                await _hubContext.Clients.Group(code).SendAsync("UpdateLobby");
                TempData["Success"] = "Hakemlikten ayrıldınız.";
            }
            else
            {
                TempData["Error"] = "Hakem değilsiniz.";
            }
            return RedirectToAction("LobbyRoom", new { code });
        }

        // Gönüllü isteği için model
        public class VolunteerRequest
        {
            public string code { get; set; }
            public string team { get; set; }
        }
    }
}