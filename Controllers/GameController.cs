using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using YourTurn.Web.Hubs;
using YourTurn.Web.Models;
using YourTurn.Web.Services;
using YourTurn.Web.Interfaces;
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
        private readonly IGameService _gameService;
        private readonly IAntiforgery _antiforgery;
        private readonly YourTurnDbContext _context;

        // Gerekli servisleri enjekte eder
        public GameController(IHubContext<GameHub> hubContext, IGameService gameService, IAntiforgery antiforgery, YourTurnDbContext context)
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
                Categories = await _context.Categories.OrderBy(c => c.Name).ToListAsync(),
                IsGameCompleted = lobby.GameState != null && _gameService.GetWinningTeam(lobby.GameState.Team1Score, lobby.GameState.Team2Score) != null,
                GameWinner = lobby.GameState != null ? _gameService.GetWinningTeam(lobby.GameState.Team1Score, lobby.GameState.Team2Score) : null
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
            if (lobby == null)
                return Json(new { success = false, message = "Lobby bulunamadı" });

            var currentPlayerName = HttpContext.Session.GetString("PlayerName");
            var player = lobby.Players.FirstOrDefault(p => p.Name == currentPlayerName);
            
            var result = await _gameService.VolunteerForTeamAsync(lobby, currentPlayerName, team);
            if (!result.success)
                return Json(new { success = false, message = result.message });

            await _hubContext.Clients.Group(code).SendAsync("UpdateGame");

            return Json(new { success = true });
        }

        // Zamanlayıcı güncellemeleri ve kazanma koşulları dahil olmak üzere mevcut oyun durumunu döndürür
        [HttpGet]
        public IActionResult GetGameState(string code)
        {
            var lobby = _gameService.FindLobby(code);
            if (lobby == null)
                return Json(new { success = false });

            var dto = _gameService.BuildAndAdvanceGameState(lobby);
            return Json(new {
                success = dto.Success,
                isWaitingForVolunteers = dto.IsWaitingForVolunteers,
                team1Volunteer = dto.Team1Volunteer,
                team2Volunteer = dto.Team2Volunteer,
                fusePosition = dto.FusePosition,
                isTimerRunning = dto.IsTimerRunning,
                currentTurn = dto.CurrentTurn,
                isGameActive = dto.IsGameActive,
                winner = dto.Winner,
                team1Score = dto.Team1Score,
                team2Score = dto.Team2Score,
                activePlayer1 = dto.ActivePlayer1,
                activePlayer2 = dto.ActivePlayer2,
                gameWinner = dto.GameWinner,
                isGameCompleted = dto.IsGameCompleted,
                question = dto.Question,
                players = dto.Players
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
            public bool IsGameCompleted { get; set; }
            public string? GameWinner { get; set; }
        }
    }
}