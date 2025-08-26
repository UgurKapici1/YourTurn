using YourTurn.Web.Models;
using YourTurn.Web.Stores;
using YourTurn.Web.Interfaces;
using YourTurn.Web.Models.Dto;
using YourTurn.Web.Interfaces;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using YourTurn.Web.Hubs;

namespace YourTurn.Web.Services
{
    // Oyunla ilgili temel mantığı ve yardımcı fonksiyonları içeren servis
    public class GameService : IGameService
    {
        private readonly IGameRepository _gameRepository;
        private readonly ISettingsService _settingsService;
        private readonly IHubContext<GameHub> _hubContext;
        private readonly ILobbyStore _lobbyStore;
        private const int HOST_TIMEOUT_SECONDS = GameConstants.HostTimeoutSeconds; // Ana bilgisayarın sinyal gönderme zaman aşımı

        public GameService(IGameRepository gameRepository, ISettingsService settingsService, IHubContext<GameHub> hubContext, ILobbyStore lobbyStore)
        {
            _gameRepository = gameRepository;
            _settingsService = settingsService;
            _hubContext = hubContext;
            _lobbyStore = lobbyStore;
        }

        // Bir lobiyi koduna göre bulur
        public Lobby? FindLobby(string code)
        {
            return _lobbyStore.GetActiveLobbies().FirstOrDefault(l => l.LobbyCode == code);
        }

        // Bir lobiyi koduna göre (büyük/küçük harf duyarsız) bulur
        public Lobby? FindLobbyIgnoreCase(string code)
        {
            return _lobbyStore.GetActiveLobbies().FirstOrDefault(l => l.LobbyCode.Equals(code, StringComparison.OrdinalIgnoreCase));
        }

        // Verilen isimle yeni bir oyuncu oluşturur
        public Player CreatePlayer(string playerName)
        {
            return new Player
            {
                Name = playerName,
                Team = ""
            };
        }

        // Bir eş ana bilgisayarın hala çevrimiçi olup olmadığını sinyaline göre kontrol eder
        public Task<bool> IsPeerHostOnlineAsync(Lobby lobby)
        {
            if (!lobby.IsPeerHosted || lobby.LastHostHeartbeat == null)
                return Task.FromResult(false);

            var timeSinceLastHeartbeat = DateTime.Now - lobby.LastHostHeartbeat.Value;
            return Task.FromResult(timeSinceLastHeartbeat.TotalSeconds < HOST_TIMEOUT_SECONDS);
        }

        // Tüm lobiler için ana bilgisayar çevrimiçi durumunu günceller
        public async Task UpdateHostStatusesAsync()
        {
            foreach (var lobby in _lobbyStore.GetActiveLobbies().Where(l => l.IsPeerHosted))
            {
                lobby.IsHostOnline = await IsPeerHostOnlineAsync(lobby);
            }
        }

        // Belirtilen kategoriden rastgele bir soru döndürür
        public async Task<Question?> GetRandomQuestionAsync(string categoryName, List<int>? excludeQuestionIds = null)
        {
            var category = await _gameRepository.GetCategoryWithQuestionsAsync(categoryName);

            if (category == null || !category.Questions.Any())
            {
                return null;
            }

            IEnumerable<Question> questions = category.Questions;
            if (excludeQuestionIds != null && excludeQuestionIds.Any())
            {
                questions = questions.Where(q => !excludeQuestionIds.Contains(q.Id));
            }

            var questionsList = questions.ToList();
            if (!questionsList.Any())
                return null;

            var random = new Random();
            int index = random.Next(questionsList.Count);
            return questionsList[index];
        }

        // Dinamik olarak kazanma skorunu getirir
        public int GetWinningScore()
        {
            return _settingsService.GetWinningScore();
        }

        // Dinamik olarak timer hızını getirir
        public double GetTimerSpeed()
        {
            return _settingsService.GetTimerSpeed();
        }

        // Bir lobi için yeni bir oyun durumu başlatır
        public async Task<GameState?> InitializeGameStateAsync(string category)
        {
            var question = await GetRandomQuestionAsync(category);
            if (question == null) return null;

            return new GameState
            {
                FusePosition = 0,
                GameStartTime = DateTime.Now,
                LastTurnStartTime = DateTime.Now,
                CurrentQuestion = question,
                Team1Score = 0,
                Team2Score = 0,
                IsGameActive = true,
                IsTimerRunning = false,
                TimerSpeed = GetTimerSpeed(),
                IsWaitingForVolunteers = true
            };
        }

        // Skorları koruyarak yeni bir tur başlatır
        public async Task<GameState?> InitializeNewRoundAsync(string category, int team1Score, int team2Score, string team1Volunteer, string team2Volunteer)
        {
            var question = await GetRandomQuestionAsync(category);
            if (question == null) return null;

            var random = new Random();
            var team1Starts = random.Next(2) == 0;
            
            return new GameState
            {
                FusePosition = 0,
                GameStartTime = DateTime.Now,
                LastTurnStartTime = DateTime.Now,
                CurrentQuestion = question,
                Team1Score = team1Score,
                Team2Score = team2Score,
                IsGameActive = true,
                IsTimerRunning = false,
                TimerSpeed = GetTimerSpeed(),
                IsWaitingForVolunteers = false,
                Team1Volunteer = team1Volunteer,
                Team2Volunteer = team2Volunteer,
                ActivePlayer1 = team1Volunteer,
                ActivePlayer2 = team2Volunteer,
                CurrentTurn = team1Starts ? team1Volunteer : team2Volunteer
            };
        }

        // Bir takımın kazanma skoruna ulaşıp ulaşmadığını kontrol eder
        public bool HasWinningTeam(int team1Score, int team2Score)
        {
            int winningScore = GetWinningScore();
            return team1Score >= winningScore || team2Score >= winningScore;
        }

        // Kazanan takımın adını alır
        public string? GetWinningTeam(int team1Score, int team2Score)
        {
            int winningScore = GetWinningScore();
            if (team1Score >= winningScore)
                return GameConstants.TeamLeft;
            if (team2Score >= winningScore)
                return GameConstants.TeamRight;
            return null;
        }

        // Oyuncu adı sağlanmamışsa rastgele bir oyuncu adı oluşturur
        public string GeneratePlayerName(string? playerName = null)
        {
            return playerName ?? "Oyuncu" + new Random().Next(1000, 9999);
        }

        // Bir lobinin oyuna başlayıp başlayamayacağını doğrular
        public (bool canStart, string? errorMessage) ValidateGameStart(Lobby lobby)
        {
            if (string.IsNullOrEmpty(lobby.Category))
            {
                return (false, "Oyunu başlatmak için önce bir kategori seçmelisiniz!");
            }

            var team1Count = lobby.Players.Count(p => p.Team == GameConstants.TeamLeft);
            var team2Count = lobby.Players.Count(p => p.Team == GameConstants.TeamRight);

            if (team1Count == 0 || team2Count == 0)
            {
                return (false, "Oyunu başlatmak için her takımda en az bir oyuncu olması gerekir!");
            }

            if (lobby.GameState == null || 
                string.IsNullOrEmpty(lobby.GameState.Team1Volunteer) || 
                string.IsNullOrEmpty(lobby.GameState.Team2Volunteer))
            {
                return (false, "Oyunu başlatmak için her takımdan bir gönüllü olması gerekir!");
            }

            return (true, null);
        }

        public async Task<object> SubmitAnswerAsync(string lobbyCode, string playerName, string answer)
        {
            var lobby = FindLobby(lobbyCode);
            if (lobby?.GameState == null)
            {
                return new { success = false, message = "Oyun bulunamadı." };
            }

            if (lobby.GameState.CurrentTurn != playerName)
            {
                return new { success = false, message = "Sıra sizde değil." };
            }

            var correctAnswer = lobby.GameState.CurrentQuestion?.Answers.FirstOrDefault(a => a.IsCorrect)?.Text;
            if (correctAnswer == null)
            {
                return new { success = false, message = "Mevcut soru için doğru cevap bulunamadı." };
            }

            var isCorrect = string.Equals(answer.Trim(), correctAnswer.Trim(), StringComparison.OrdinalIgnoreCase);

            if (isCorrect)
            {
                lobby.GameState.IsTimerRunning = false;

                // Turnu kimin pasladığına göre fitili hareket ettir
                if (lobby.GameState.CurrentTurn == lobby.GameState.ActivePlayer1)
                {
                    lobby.GameState.FusePosition += GameConstants.CorrectAnswerStep; 
                }
                else
                {
                    lobby.GameState.FusePosition -= GameConstants.CorrectAnswerStep;
                }
                lobby.GameState.FusePosition = Math.Clamp(lobby.GameState.FusePosition, GameConstants.FuseMin, GameConstants.FuseMax);


                // Sıradaki oyuncuya geç
                lobby.GameState.CurrentTurn = lobby.GameState.CurrentTurn == lobby.GameState.ActivePlayer1
                    ? lobby.GameState.ActivePlayer2
                    : lobby.GameState.ActivePlayer1;

                // Yeni soru al
                var excludeIds = lobby.GameState.AskedQuestionIds ?? new List<int>();
                if (lobby.GameState.CurrentQuestion != null && !excludeIds.Contains(lobby.GameState.CurrentQuestion.Id))
                {
                    excludeIds.Add(lobby.GameState.CurrentQuestion.Id);
                }
                var newQuestion = await GetRandomQuestionAsync(lobby.Category, excludeIds);

                if (newQuestion == null)
                {
                    // Kategoriye ait soru kalmadı, round biter
                    string kazananTakim;
                    if (lobby.GameState.FusePosition < 0) { // Sol'a daha yakınsa, Sağ kazanır
                        kazananTakim = GameConstants.TeamRight;
                        lobby.GameState.Team2Score++;
                    } else { // Sağ'a daha yakınsa veya ortadaysa, Sol kazanır
                        kazananTakim = GameConstants.TeamLeft;
                        lobby.GameState.Team1Score++;
                    }
                    lobby.GameState.Winner = kazananTakim;
                    lobby.GameState.IsGameActive = false;
                    lobby.GameState.RoundEndMessage = "Bu kategorideki tüm sorular cevaplandı! Fuse pozisyonuna göre round'un galibi belirlendi.";
                }
                else
                {
                    lobby.GameState.CurrentQuestion = newQuestion;
                    lobby.GameState.AskedQuestionIds = excludeIds;
                    lobby.GameState.LastTurnStartTime = DateTime.Now;
                    lobby.GameState.IsTimerRunning = true; // Yeni soruyla zamanı başlat
                }

                await _hubContext.Clients.Group(lobbyCode).SendAsync("UpdateGame");
                return new { success = true, isCorrect = true };
            }
            else
            {
                // Yanlış cevap
                return new { success = true, isCorrect = false, message = "Yanlış cevap, tekrar deneyin!" };
            }
        }

        public async Task ResetGameAsync(string code, string hostPlayerName)
        {
            var lobby = FindLobby(code);
            if (lobby == null) return;

            if (lobby.HostPlayerName != hostPlayerName) return;

            lobby.IsGameStarted = false;
            lobby.GameState = null;

            await _hubContext.Clients.Group(code).SendAsync("GameReset");
        }

        // Controller'daki GetGameState mantığını kapsüller
        public GameStateDto BuildAndAdvanceGameState(Lobby lobby)
        {
            if (lobby.GameState == null)
            {
                return new GameStateDto { Success = false };
            }

            var gs = lobby.GameState;

            if (gs.IsWaitingForVolunteers)
            {
                return new GameStateDto
                {
                    Success = true,
                    IsWaitingForVolunteers = true,
                    Team1Volunteer = gs.Team1Volunteer,
                    Team2Volunteer = gs.Team2Volunteer
                };
            }

            if (gs.IsTimerRunning && gs.LastTurnStartTime.HasValue)
            {
                var elapsedSeconds = (DateTime.Now - gs.LastTurnStartTime.Value).TotalSeconds;
                var movement = elapsedSeconds * gs.TimerSpeed;

                if (gs.CurrentTurn == gs.ActivePlayer1)
                {
                    gs.FusePosition = Math.Max(GameConstants.FuseMin, gs.FusePosition - movement);
                }
                else
                {
                    gs.FusePosition = Math.Min(GameConstants.FuseMax, gs.FusePosition + movement);
                }

                if (gs.FusePosition <= GameConstants.FuseMin)
                {
                    gs.Winner = GameConstants.TeamRight;
                    gs.Team2Score++;
                    gs.IsGameActive = false;
                    gs.IsTimerRunning = false;
                }
                else if (gs.FusePosition >= GameConstants.FuseMax)
                {
                    gs.Winner = GameConstants.TeamLeft;
                    gs.Team1Score++;
                    gs.IsGameActive = false;
                    gs.IsTimerRunning = false;
                }
            }

            var gameWinner = GetWinningTeam(gs.Team1Score, gs.Team2Score);
            var isGameCompleted = gameWinner != null;

            var dto = new GameStateDto
            {
                Success = true,
                IsWaitingForVolunteers = false,
                FusePosition = gs.FusePosition,
                IsTimerRunning = gs.IsTimerRunning,
                CurrentTurn = gs.CurrentTurn,
                IsGameActive = gs.IsGameActive,
                Winner = gs.Winner,
                Team1Score = gs.Team1Score,
                Team2Score = gs.Team2Score,
                ActivePlayer1 = gs.ActivePlayer1,
                ActivePlayer2 = gs.ActivePlayer2,
                GameWinner = gameWinner,
                IsGameCompleted = isGameCompleted,
                Question = gs.CurrentQuestion?.Text
            };

            dto.Players = lobby.Players.Select(p => new PlayerDto { Name = p.Name, Team = p.Team }).ToList();

            return dto;
        }

        public Task<(bool success, string? message)> VolunteerForTeamAsync(Lobby lobby, string currentPlayerName, string team)
        {
            if (lobby.IsGameStarted)
                return Task.FromResult((false, "Oyun başladıktan sonra gönüllü değiştirilemez."));

            var player = lobby.Players.FirstOrDefault(p => p.Name == currentPlayerName);
            if (player == null || player.Team != team)
                return Task.FromResult((false, "Bu takım için gönüllü olamazsınız."));

            lobby.GameState ??= new GameState();

            if (team == "Sol")
            {
                if (!string.IsNullOrEmpty(lobby.GameState.Team1Volunteer))
                    return Task.FromResult((false, "Bu takımda zaten gönüllü var."));
                lobby.GameState.Team1Volunteer = currentPlayerName;
            }
            else if (team == "Sağ")
            {
                if (!string.IsNullOrEmpty(lobby.GameState.Team2Volunteer))
                    return Task.FromResult((false, "Bu takımda zaten gönüllü var."));
                lobby.GameState.Team2Volunteer = currentPlayerName;
            }
            else
            {
                return Task.FromResult((false, "Geçersiz takım."));
            }

            return Task.FromResult((true, (string?)null));
        }

        public Task<(bool success, string? message)> WithdrawVolunteerAsync(Lobby lobby, string currentPlayerName, string team)
        {
            if (lobby.IsGameStarted)
                return Task.FromResult((false, "Oyun başladıktan sonra gönüllü geri çekilemez."));

            if (lobby.GameState == null)
                return Task.FromResult((true, (string?)null));

            if (team == "Sol" && lobby.GameState.Team1Volunteer == currentPlayerName)
            {
                lobby.GameState.Team1Volunteer = null;
            }
            else if (team == "Sağ" && lobby.GameState.Team2Volunteer == currentPlayerName)
            {
                lobby.GameState.Team2Volunteer = null;
            }
            else
            {
                return Task.FromResult((false, "Bu takımdan gönüllülüğünüzü geri çekemezsiniz."));
            }

            return Task.FromResult((true, (string?)null));
        }
    }
} 