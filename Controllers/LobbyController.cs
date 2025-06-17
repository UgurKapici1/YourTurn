using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using YourTurn.Web.Hubs;
using YourTurn.Web.Models;
using YourTurn.Web.Services;
using YourTurn.Web.Stores;

namespace YourTurn.Web.Controllers
{
    public class LobbyController : Controller
    {
        private readonly IHubContext<LobbyHub> _hubContext;

        public LobbyController(IHubContext<LobbyHub> hubContext)
        {
            _hubContext = hubContext;
        }

        /// <summary>
        /// Creates a new lobby with the current player as host
        /// </summary>
        [HttpPost]
        public IActionResult Create()
        {
            string playerName = GameService.GeneratePlayerName(TempData["PlayerName"]?.ToString());

            var newLobby = new Lobby
            {
                HostPlayerName = playerName
            };

            newLobby.Players.Add(GameService.CreatePlayer(playerName));

            LobbyStore.ActiveLobbies.Add(newLobby);
            HttpContext.Session.SetString("PlayerName", playerName);

            return RedirectToAction("LobbyRoom", new { code = newLobby.LobbyCode });
        }

        /// <summary>
        /// Allows a player to join an existing lobby
        /// </summary>
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

        /// <summary>
        /// Displays the lobby room for a specific lobby
        /// </summary>
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

        /// <summary>
        /// Allows the host to choose a category for the game
        /// </summary>
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

        /// <summary>
        /// Allows a player to choose or change their team
        /// </summary>
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

        /// <summary>
        /// Allows a player to leave their current team
        /// </summary>
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

        /// <summary>
        /// Allows a player to leave the lobby
        /// </summary>
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
                    lobby.Players.Remove(player);
                    
                    if (lobby.Players.Count == 0)
                    {
                        LobbyStore.ActiveLobbies.Remove(lobby);
                    }
                    else
                    {
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

        /// <summary>
        /// Starts the game if all conditions are met
        /// </summary>
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

        /// <summary>
        /// Allows a player to volunteer for their team
        /// </summary>
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

        /// <summary>
        /// Allows a player to withdraw their volunteer status
        /// </summary>
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

        public class VolunteerRequest
        {
            public string code { get; set; }
            public string team { get; set; }
        }
    }
}