using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using YourTurn.Web.Data;
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
        private readonly GameService _gameService;
        private readonly YourTurnDbContext _context;

        // Gerekli servisleri enjekte eder
        public LobbyController(IHubContext<LobbyHub> hubContext, GameService gameService, YourTurnDbContext context)
        {
            _hubContext = hubContext;
            _gameService = gameService;
            _context = context;
        }

        // Mevcut oyuncuyla yeni bir lobi oluşturur ve ana bilgisayar olarak ayarlar
        [HttpPost]
        public async Task<IActionResult> Create()
        {
            string playerName = _gameService.GeneratePlayerName(TempData["PlayerName"]?.ToString());

            var newLobby = new Lobby
            {
                HostPlayerName = playerName
            };

            newLobby.Players.Add(_gameService.CreatePlayer(playerName));

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
            string playerName = _gameService.GeneratePlayerName(TempData["PlayerName"]?.ToString());

            var lobby = _gameService.FindLobbyIgnoreCase(lobbyCode);
            if (lobby == null)
            {
                ViewBag.Error = "Lobi bulunamadı!";
                return RedirectToAction("JoinLobby", "Home");
            }

            if (!lobby.Players.Any(p => p.Name == playerName))
            {
                lobby.Players.Add(_gameService.CreatePlayer(playerName));
            }
            HttpContext.Session.SetString("PlayerName", playerName);
            await _hubContext.Clients.Group(lobbyCode).SendAsync("UpdateLobby");

            return RedirectToAction("LobbyRoom", new { code = lobby.LobbyCode });
        }

        // Belirli bir lobi için lobi odasını görüntüler
        [HttpGet]
        public async Task<IActionResult> LobbyRoom(string code)
        {
            var lobby = _gameService.FindLobby(code);
            if (lobby == null)
                return NotFound("Lobby bulunamadı");

            if (lobby.IsGameStarted)
            {
                return RedirectToAction("Game", "Game", new { code });
            }

            var viewModel = new LobbyRoomViewModel
            {
                Lobby = lobby,
                Categories = await _context.Categories.OrderBy(c => c.Name).ToListAsync()
            };

            ViewBag.CurrentPlayerName = HttpContext.Session.GetString("PlayerName");

            return View(viewModel);
        }

        // Bir lobi için eş ana bilgisayar bilgilerini alır
        [HttpGet]
        public IActionResult GetPeerHostInfo(string code)
        {
            var lobby = _gameService.FindLobby(code);
            if (lobby == null)
                return NotFound("Lobby bulunamadı");

            if (!lobby.IsPeerHosted) // Sadece IsPeerHosted kontrolü yeterli olabilir
                return NotFound("Bu lobi P2P barındırma için ayarlanmamış.");

            var currentPlayerName = HttpContext.Session.GetString("PlayerName");

            return Json(new
            {
                hostName = lobby.HostPlayerName,
                isHost = lobby.HostPlayerName == currentPlayerName,
                hostIP = lobby.HostIPAddress,
                hostPort = lobby.HostPort,
                isOnline = lobby.IsHostOnline
            });
        }

        // Ana bilgisayarın oyun için bir kategori seçmesine izin verir
        [HttpPost]
        public async Task<IActionResult> ChooseCategory(string code, string category)
        {
            var lobby = _gameService.FindLobby(code);
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
            var lobby = _gameService.FindLobby(code);
            if (lobby == null || lobby.IsGameStarted)
                return Forbid();

            var currentPlayerName = HttpContext.Session.GetString("PlayerName");
            if (currentPlayerName != playerName)
            {
                return Forbid("Başka bir oyuncu adına işlem yapamazsınız.");
            }

            // Hakemin takıma katılmasını engelle
            if (lobby.RefereeName == playerName)
            {
                TempData["Error"] = "Hakemler bir takıma katılamaz. Takıma katılmak için hakemlikten ayrılmalısınız.";
                return RedirectToAction("LobbyRoom", new { code });
            }

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
            var lobby = _gameService.FindLobby(code);
            if (lobby == null || lobby.IsGameStarted)
                return NotFound();

            var currentPlayerName = HttpContext.Session.GetString("PlayerName");
            if (currentPlayerName != playerName)
            {
                return Forbid("Başka bir oyuncu adına işlem yapamazsınız.");
            }

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
            var lobby = _gameService.FindLobby(code);
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
            var lobby = _gameService.FindLobby(code);
            if (lobby == null)
                return NotFound("Lobby not found.");

            var currentPlayerName = HttpContext.Session.GetString("PlayerName");
            if (lobby.HostPlayerName != currentPlayerName)
            {
                return Forbid("Only the host can start the game.");
            }

            var (canStart, errorMessage) = _gameService.ValidateGameStart(lobby);
            if (!canStart)
            {
                TempData["Error"] = errorMessage;
                return RedirectToAction("LobbyRoom", new { code });
            }

            lobby.IsGameStarted = true;
            lobby.GameState = await _gameService.InitializeNewRoundAsync(
                lobby.Category,
                0, 0,
                lobby.GameState.Team1Volunteer,
                lobby.GameState.Team2Volunteer);
            
            if (lobby.GameState == null)
            {
                 TempData["Error"] = "Oyun başlatılamadı. Seçilen kategori için soru bulunamadı.";
                 lobby.IsGameStarted = false;
                 return RedirectToAction("LobbyRoom", new { code });
            }

            await _hubContext.Clients.Group(code).SendAsync("GameStarted", code);
            return Ok();
        }

        // Bir oyuncunun takımı için gönüllü olmasına izin verir
        [HttpPost]
        public async Task<IActionResult> VolunteerForTeam([FromBody] VolunteerRequest request)
        {
            var lobby = _gameService.FindLobby(request.code);
            if (lobby == null || lobby.IsGameStarted)
                return NotFound();
            
            var currentPlayerName = HttpContext.Session.GetString("PlayerName");
            var player = lobby.Players.FirstOrDefault(p => p.Name == currentPlayerName);
            if (player == null || player.Team != request.team)
            {
                return Forbid("Bu takım için gönüllü olamazsınız.");
            }

            if (lobby.GameState == null)
            {
                lobby.GameState = new GameState();
            }

            if (request.team == "Sol")
            {
                if (string.IsNullOrEmpty(lobby.GameState.Team1Volunteer))
                {
                    lobby.GameState.Team1Volunteer = currentPlayerName;
                }
                else
                {
                    return BadRequest("Bu takımın zaten bir gönüllüsü var.");
                }
            }
            else if (request.team == "Sağ")
            {
                if (string.IsNullOrEmpty(lobby.GameState.Team2Volunteer))
                {
                    lobby.GameState.Team2Volunteer = currentPlayerName;
                }
                else
                {
                    return BadRequest("Bu takımın zaten bir gönüllüsü var.");
                }
            }
            else
            {
                return BadRequest("Geçersiz takım.");
            }

            await _hubContext.Clients.Group(request.code).SendAsync("UpdateLobby");
            return Ok();
        }

        // Gönüllünün geri çekilmesine izin verir
        [HttpPost]
        public async Task<IActionResult> WithdrawVolunteer([FromBody] VolunteerRequest request)
        {
            var lobby = _gameService.FindLobby(request.code);
            if (lobby == null || lobby.IsGameStarted)
                return NotFound();

            if (lobby.GameState == null) return Ok();

            var currentPlayerName = HttpContext.Session.GetString("PlayerName");
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
                return Forbid("Bu takımdan gönüllülüğünüzü geri çekemezsiniz.");
            }

            await _hubContext.Clients.Group(request.code).SendAsync("UpdateLobby");
            return Ok();
        }

        // Takımları rastgele dağıtır
        [HttpPost]
        public async Task<IActionResult> RandomizeTeams(string code)
        {
            var lobby = _gameService.FindLobby(code);
            if (lobby == null || lobby.IsGameStarted)
                return NotFound();
            
            var currentPlayerName = HttpContext.Session.GetString("PlayerName");
            if (lobby.HostPlayerName != currentPlayerName)
            {
                return Forbid("Sadece ev sahibi takımları karıştırabilir.");
            }

            var playersToAssign = lobby.Players.Where(p => string.IsNullOrEmpty(p.Team) && p.Name != lobby.RefereeName).ToList();
            if (playersToAssign.Count == 0)
                return RedirectToAction("LobbyRoom", new { code });

            var random = new Random();
            playersToAssign = playersToAssign.OrderBy(p => random.Next()).ToList();

            var team1Count = lobby.Players.Count(p => p.Team == "Sol");
            var team2Count = lobby.Players.Count(p => p.Team == "Sağ");

            foreach (var player in playersToAssign)
            {
                if (team1Count <= team2Count)
                {
                    player.Team = "Sol";
                    team1Count++;
                }
                else
                {
                    player.Team = "Sağ";
                    team2Count++;
                }
            }
            
            await _hubContext.Clients.Group(code).SendAsync("UpdateLobby");
            return RedirectToAction("LobbyRoom", new { code });
        }

        // Takımları sıfırlar
        [HttpPost]
        public async Task<IActionResult> ResetTeams(string code)
        {
            var lobby = _gameService.FindLobby(code);
            if (lobby == null || lobby.IsGameStarted)
                return NotFound();

            var currentPlayerName = HttpContext.Session.GetString("PlayerName");
            if (lobby.HostPlayerName != currentPlayerName)
            {
                return Forbid("Sadece ev sahibi takımları sıfırlayabilir.");
            }

            foreach (var player in lobby.Players)
            {
                player.Team = "";
            }
            
            if (lobby.GameState != null)
            {
                lobby.GameState.Team1Volunteer = null;
                lobby.GameState.Team2Volunteer = null;
            }

            await _hubContext.Clients.Group(code).SendAsync("UpdateLobby");
            return RedirectToAction("LobbyRoom", new { code });
        }

        // Oyuncunun hakem olmasını sağlar
        [HttpPost]
        public async Task<IActionResult> BecomeReferee(string code)
        {
            var lobby = _gameService.FindLobby(code);
            if (lobby == null || lobby.IsGameStarted)
                return NotFound();

            var currentPlayerName = HttpContext.Session.GetString("PlayerName");

            if (!string.IsNullOrEmpty(lobby.RefereeName))
            {
                TempData["Error"] = "Bu lobide zaten bir hakem var.";
                return RedirectToAction("LobbyRoom", new { code });
            }

            var player = lobby.Players.FirstOrDefault(p => p.Name == currentPlayerName);
            if (player != null && !string.IsNullOrEmpty(player.Team))
            {
                TempData["Error"] = "Hakem olmak için önce takımınızdan ayrılmalısınız.";
                return RedirectToAction("LobbyRoom", new { code });
            }

            lobby.RefereeName = currentPlayerName;

            await _hubContext.Clients.Group(code).SendAsync("UpdateLobby");
            return RedirectToAction("LobbyRoom", new { code });
        }

        // Hakemin görevini bırakmasını sağlar
        [HttpPost]
        public async Task<IActionResult> LeaveReferee(string code)
        {
            var lobby = _gameService.FindLobby(code);
            if (lobby == null || lobby.IsGameStarted)
                return NotFound();
            
            var currentPlayerName = HttpContext.Session.GetString("PlayerName");
            if (lobby.RefereeName != currentPlayerName)
            {
                return Forbid("Sadece mevcut hakem bu rolden ayrılabilir.");
            }

            lobby.RefereeName = null;

            await _hubContext.Clients.Group(code).SendAsync("UpdateLobby");
            return RedirectToAction("LobbyRoom", new { code });
        }

        // Gönüllü isteği için model
        public class VolunteerRequest
        {
            public string code { get; set; }
            public string team { get; set; }
        }
        
        public class LobbyRoomViewModel
        {
            public Lobby Lobby { get; set; }
            public List<Category> Categories { get; set; } = new List<Category>();
        }
    }
}