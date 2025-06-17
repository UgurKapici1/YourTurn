using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using YourTurn.Web.Hubs;
using YourTurn.Web.Models;
using YourTurn.Web.Services;

namespace YourTurn.Web.Controllers
{
    public class GameController : Controller
    {
        private readonly IHubContext<GameHub> _hubContext;

        public GameController(IHubContext<GameHub> hubContext)
        {
            _hubContext = hubContext;
        }

        /// <summary>
        /// Displays the game page for a specific lobby
        /// </summary>
        [HttpGet]
        public IActionResult Game(string code)
        {
            var lobby = GameService.FindLobby(code);
            if (lobby == null)
                return NotFound("Lobby bulunamadı");

            if (!lobby.IsGameStarted)
                return RedirectToAction("LobbyRoom", "Lobby", new { code });

            if (lobby.GameState == null)
            {
                lobby.GameState = GameService.InitializeGameState(lobby.Category);
            }

            ViewBag.CurrentPlayerName = HttpContext.Session.GetString("PlayerName");
            return View(lobby);
        }

        /// <summary>
        /// Allows the current player to pass their turn to the next player
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> PassTurn(string code)
        {
            try
            {
                var lobby = GameService.FindLobby(code);
                if (lobby == null || lobby.GameState == null)
                    return Json(new { success = false, message = "Lobby veya oyun durumu bulunamadı" });

                var currentPlayerName = HttpContext.Session.GetString("PlayerName");
                
                if (lobby.GameState.CurrentTurn != currentPlayerName)
                    return Json(new { success = false, message = "Sıra sizde değil" });

                lobby.GameState.IsTimerRunning = false;

                if (lobby.GameState.CurrentTurn == lobby.GameState.ActivePlayer1)
                {
                    lobby.GameState.CurrentTurn = lobby.GameState.ActivePlayer2;
                }
                else
                {
                    lobby.GameState.CurrentTurn = lobby.GameState.ActivePlayer1;
                }

                lobby.GameState.CurrentQuestion = GameService.GetRandomQuestion(lobby.Category);
                lobby.GameState.LastAnswerTime = DateTime.Now;
                lobby.GameState.LastTurnStartTime = DateTime.Now;
                lobby.GameState.IsTimerRunning = true;

                await _hubContext.Clients.Group(code).SendAsync("UpdateGame");

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Allows a player to volunteer for their team
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> VolunteerForTeam(string code, string team)
        {
            try
            {
                var lobby = GameService.FindLobby(code);
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
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Returns the current game state including timer updates and win conditions
        /// </summary>
        [HttpGet]
        public IActionResult GetGameState(string code)
        {
            var lobby = GameService.FindLobby(code);
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

                // Check for round winner
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

            // Check for game winner (5 points)
            var gameWinner = GameService.GetWinningTeam(lobby.GameState.Team1Score, lobby.GameState.Team2Score);
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
                isGameCompleted = isGameCompleted
            });
        }

        /// <summary>
        /// Starts a new round by resetting the game state and returning to lobby
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> StartNewRound(string code)
        {
            var lobby = GameService.FindLobby(code);
            if (lobby == null || lobby.GameState == null)
                return NotFound();

            // Check if game is completed (5 points reached)
            if (GameService.HasWinningTeam(lobby.GameState.Team1Score, lobby.GameState.Team2Score))
            {
                // Game is completed, return to lobby
                lobby.IsGameStarted = false;
                lobby.GameState = null;
                await _hubContext.Clients.Group(code).SendAsync("UpdateGame");
                return RedirectToAction("LobbyRoom", "Lobby", new { code });
            }

            // Start new round with preserved scores
            var team1Volunteer = lobby.GameState.Team1Volunteer;
            var team2Volunteer = lobby.GameState.Team2Volunteer;
            
            lobby.GameState = GameService.InitializeNewRound(
                lobby.Category, 
                lobby.GameState.Team1Score, 
                lobby.GameState.Team2Score,
                team1Volunteer,
                team2Volunteer
            );

            // Add a message about which team starts
            var startingTeam = lobby.GameState.CurrentTurn == team1Volunteer ? "Sol" : "Sağ";
            TempData["RoundStartMessage"] = $"🎲 Rastgele seçim sonucu {startingTeam} takımı başlıyor!";

            await _hubContext.Clients.Group(code).SendAsync("UpdateGame");
            
            return RedirectToAction("Game", new { code });
        }

        /// <summary>
        /// Changes the category during a game break
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> ChangeCategory(string code, string category)
        {
            var lobby = GameService.FindLobby(code);
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

        /// <summary>
        /// Allows the host to reset the game and return to lobby
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> ResetGame(string code)
        {
            try
            {
                var lobby = GameService.FindLobby(code);
                if (lobby == null)
                    return Json(new { success = false, message = "Lobby bulunamadı" });

                var currentPlayerName = HttpContext.Session.GetString("PlayerName");
                
                // Only host can reset the game
                if (lobby.HostPlayerName != currentPlayerName)
                    return Json(new { success = false, message = "Sadece host oyunu sıfırlayabilir" });

                // Reset game state
                lobby.IsGameStarted = false;
                lobby.GameState = null;

                // Notify all players that game is reset
                await _hubContext.Clients.Group(code).SendAsync("GameReset");

                return Json(new { success = true, redirectUrl = Url.Action("LobbyRoom", "Lobby", new { code }) });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Starts a new round with the same or new category
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> StartNewRoundWithSameOrNewCategory(string code, string category)
        {
            var lobby = GameService.FindLobby(code);
            if (lobby == null)
                return NotFound();

            var currentPlayerName = HttpContext.Session.GetString("PlayerName");
            
            // Only host can start a new round with the same or new category
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

            // Start new round with preserved scores
            var team1Volunteer = lobby.GameState.Team1Volunteer;
            var team2Volunteer = lobby.GameState.Team2Volunteer;
            
            lobby.GameState = GameService.InitializeNewRound(
                lobby.Category, 
                lobby.GameState.Team1Score, 
                lobby.GameState.Team2Score,
                team1Volunteer,
                team2Volunteer
            );

            // Add a message about which team starts
            var startingTeam = lobby.GameState.CurrentTurn == team1Volunteer ? "Sol" : "Sağ";
            TempData["RoundStartMessage"] = $"🎲 Rastgele seçim sonucu {startingTeam} takımı başlıyor!";

            await _hubContext.Clients.Group(code).SendAsync("UpdateGame");
            
            return RedirectToAction("Game", new { code });
        }
    }
}