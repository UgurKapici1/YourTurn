using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using YourTurn.Web.Hubs;
using YourTurn.Web.Models;
using YourTurn.Web.Services;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.EntityFrameworkCore;
using YourTurn.Web.Data;

namespace YourTurn.Web.Controllers
{
    // Oyunla ilgili istekleri yönetir
    public class GameController : Controller
    {
        private readonly IHubContext<GameHub> _hubContext;
        private readonly GameService _gameService;
        private readonly IAntiforgery _antiforgery;
        private readonly YourTurnDbContext _context;

        // Gerekli servisleri enjekte eder
        public GameController(IHubContext<GameHub> hubContext, GameService gameService, IAntiforgery antiforgery, YourTurnDbContext context)
        {
            _hubContext = hubContext;
            _gameService = gameService;
            _antiforgery = antiforgery;
            _context = context;
        }

        // Belirtilen lobi kodu için oyun sayfasını görüntüler
        [HttpGet]
        public async Task<IActionResult> Game(string code)
        {
            var lobby = _gameService.FindLobby(code);
            if (lobby == null)
            {
                return RedirectToAction("Index", "Home");
            }

            if (!lobby.IsGameStarted)
            {
                // Oyun henüz başlamadıysa lobiye yönlendir
                return RedirectToAction("LobbyRoom", "Lobby", new { code });
            }
            
            var viewModel = new GameViewModel
            {
                Lobby = lobby,
                Categories = await _context.Categories.OrderBy(c => c.Name).ToListAsync()
            };

            ViewBag.CurrentPlayerName = HttpContext.Session.GetString("PlayerName");
            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitAnswer(string code, string answer)
        {
            var lobby = _gameService.FindLobby(code);
            if (lobby == null || lobby.GameState == null)
            {
                return Json(new { success = false, message = "Lobi veya oyun durumu bulunamadı." });
            }

            var currentPlayerName = HttpContext.Session.GetString("PlayerName");
            if (string.IsNullOrEmpty(currentPlayerName))
            {
                return Json(new { success = false, message = "Oturum bilgisi bulunamadı." });
            }

            var result = await _gameService.SubmitAnswerAsync(code, currentPlayerName, answer);

            return Json(result);
        }

        // Bir oyuncunun takımı için gönüllü olmasını sağlar
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VolunteerForTeam(string code, string team)
        {
            var lobby = _gameService.FindLobby(code);
            if (lobby == null || lobby.GameState == null)
                return Json(new { success = false, message = "Lobby veya oyun durumu bulunamadı" });

            var currentPlayerName = HttpContext.Session.GetString("PlayerName");
            var player = lobby.Players.FirstOrDefault(p => p.Name == currentPlayerName);
            
            if (player == null || player.Team != team)
                return Json(new { success = false, message = "Geçersiz oyuncu veya takım" });

            if (team == "Sol")
            {
                lobby.GameState.Team1Volunteer = currentPlayerName;
            }
            else if (team == "Sağ")
            {
                lobby.GameState.Team2Volunteer = currentPlayerName;
            }

            if (!string.IsNullOrEmpty(lobby.GameState.Team1Volunteer) && 
                !string.IsNullOrEmpty(lobby.GameState.Team2Volunteer))
            {
                lobby.GameState.ActivePlayer1 = lobby.GameState.Team1Volunteer;
                lobby.GameState.ActivePlayer2 = lobby.GameState.Team2Volunteer;
                lobby.GameState.CurrentTurn = lobby.GameState.ActivePlayer1;
                lobby.GameState.IsWaitingForVolunteers = false;
            }

            await _hubContext.Clients.Group(code).SendAsync("UpdateGame");

            return Json(new { success = true });
        }

        // Zamanlayıcı güncellemeleri ve kazanma koşulları dahil olmak üzere mevcut oyun durumunu döndürür
        [HttpGet]
        public IActionResult GetGameState(string code)
        {
            var lobby = _gameService.FindLobby(code);
            if (lobby == null || lobby.GameState == null)
                return Json(new { success = false });

            if (lobby.GameState.IsWaitingForVolunteers)
            {
                return Json(new {
                    success = true,
                    isWaitingForVolunteers = true,
                    team1Volunteer = lobby.GameState.Team1Volunteer,
                    team2Volunteer = lobby.GameState.Team2Volunteer
                });
            }

            if (lobby.GameState.IsTimerRunning && lobby.GameState.LastTurnStartTime.HasValue)
            {
                var elapsedSeconds = (DateTime.Now - lobby.GameState.LastTurnStartTime.Value).TotalSeconds;
                var movement = elapsedSeconds * lobby.GameState.TimerSpeed;

                if (lobby.GameState.CurrentTurn == lobby.GameState.ActivePlayer1)
                {
                    lobby.GameState.FusePosition = Math.Max(-100, lobby.GameState.FusePosition - movement);
                }
                else
                {
                    lobby.GameState.FusePosition = Math.Min(100, lobby.GameState.FusePosition + movement);
                }

                if (lobby.GameState.FusePosition <= -100)
                {
                    lobby.GameState.Winner = "Sağ";
                    lobby.GameState.Team2Score++;
                    lobby.GameState.IsGameActive = false;
                    lobby.GameState.IsTimerRunning = false;
                }
                else if (lobby.GameState.FusePosition >= 100)
                {
                    lobby.GameState.Winner = "Sol";
                    lobby.GameState.Team1Score++;
                    lobby.GameState.IsGameActive = false;
                    lobby.GameState.IsTimerRunning = false;
                }
            }
            
            var gameWinner = _gameService.GetWinningTeam(lobby.GameState.Team1Score, lobby.GameState.Team2Score);
            var isGameCompleted = gameWinner != null;

            return Json(new {
                success = true,
                isWaitingForVolunteers = false,
                fusePosition = lobby.GameState.FusePosition,
                isTimerRunning = lobby.GameState.IsTimerRunning,
                currentTurn = lobby.GameState.CurrentTurn,
                isGameActive = lobby.GameState.IsGameActive,
                winner = lobby.GameState.Winner,
                team1Score = lobby.GameState.Team1Score,
                team2Score = lobby.GameState.Team2Score,
                activePlayer1 = lobby.GameState.ActivePlayer1,
                activePlayer2 = lobby.GameState.ActivePlayer2,
                gameWinner = gameWinner,
                isGameCompleted = isGameCompleted,
                question = lobby.GameState.CurrentQuestion?.Text,
                players = lobby.Players.Select(p => new { p.Name, p.Team }).ToList()
            });
        }

        // Oyun durumunu sıfırlayarak ve lobiye dönerek yeni bir tur başlatır
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> StartNewRound(string code, string newCategoryName)
        {
            var lobby = _gameService.FindLobby(code);
            if (lobby == null || lobby.GameState == null)
                return NotFound();

            if (_gameService.HasWinningTeam(lobby.GameState.Team1Score, lobby.GameState.Team2Score))
            {
                lobby.IsGameStarted = false;
                lobby.GameState = null;
                await _hubContext.Clients.Group(code).SendAsync("UpdateGame");
                return RedirectToAction("LobbyRoom", "Lobby", new { code });
            }

            // Kategoriyi sadece yeni bir kategori seçildiyse güncelle
            if (!string.IsNullOrEmpty(newCategoryName))
            {
                lobby.Category = newCategoryName;
            }

            var team1Volunteer = lobby.GameState.Team1Volunteer;
            var team2Volunteer = lobby.GameState.Team2Volunteer;
            
            lobby.GameState = await _gameService.InitializeNewRoundAsync(
                lobby.Category, 
                lobby.GameState.Team1Score, 
                lobby.GameState.Team2Score,
                team1Volunteer,
                team2Volunteer
            );
            
            if(lobby.GameState == null)
            {
                 TempData["Error"] = "Yeni tur başlatılamadı. Seçilen kategori için soru bulunamadı.";
                 lobby.IsGameStarted = false;
                 return RedirectToAction("LobbyRoom", "Lobby", new { code });
            }

            // Yeni turda sorulan sorular listesini sıfırla ve ilk soruyu ekle
            if (lobby.GameState != null && lobby.GameState.CurrentQuestion != null)
            {
                lobby.GameState.AskedQuestionIds = new List<int> { lobby.GameState.CurrentQuestion.Id };
            }

            var startingTeam = lobby.GameState.CurrentTurn == team1Volunteer ? "Sol" : "Sağ";
            TempData["RoundStartMessage"] = $"🎲 Rastgele seçim sonucu {startingTeam} takımı başlıyor!";

            await _hubContext.Clients.Group(code).SendAsync("NewRoundStarted");
            
            return RedirectToAction("Game", new { code });
        }

        // Oyun kategorisini değiştirir
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangeCategory(string code, string category)
        {
            var lobby = _gameService.FindLobby(code);
            if (lobby == null)
                return NotFound();

            var currentPlayerName = HttpContext.Session.GetString("PlayerName");
            
            // Only host can change category
            if (lobby.HostPlayerName != currentPlayerName)
                return Forbid();

            // Only allow category change when game is not active
            if (lobby.GameState?.IsGameActive == true)
                return Forbid();

            // If category is empty, keep the current one
            if (!string.IsNullOrEmpty(category))
            {
                lobby.Category = category;
            }

            await _hubContext.Clients.Group(code).SendAsync("UpdateGame");
            
            return RedirectToAction("Game", new { code });
        }

        // Oyunu tamamen sıfırlar
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetGame(string code)
        {
            var hostPlayerName = HttpContext.Session.GetString("PlayerName");
            if (string.IsNullOrEmpty(hostPlayerName))
                return Json(new { success = false, message = "Oturum bulunamadı." });
            
            await _gameService.ResetGameAsync(code, hostPlayerName);
            return Json(new { success = true });
        }

        // Zamanlayıcıyı durdurur veya başlatır
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleTimer(string code)
        {
            var lobby = _gameService.FindLobby(code);
            if (lobby == null || lobby.GameState == null || lobby.HostPlayerName != HttpContext.Session.GetString("PlayerName"))
                return Json(new { success = false, message = "Yetkisiz işlem veya oyun bulunamadı" });

            lobby.GameState.IsTimerRunning = !lobby.GameState.IsTimerRunning;
            if (lobby.GameState.IsTimerRunning)
            {
                lobby.GameState.LastTurnStartTime = DateTime.Now;
            }
            
            await _hubContext.Clients.Group(code).SendAsync("UpdateGame");
            return Json(new { success = true });
        }

        public class GameViewModel
        {
            public Lobby Lobby { get; set; }
            public List<Category> Categories { get; set; } = new List<Category>();
        }
    }
}