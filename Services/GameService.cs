using YourTurn.Web.Models;
using YourTurn.Web.Stores;
using YourTurn.Web.Data;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using YourTurn.Web.Hubs;

namespace YourTurn.Web.Services
{
    // Oyunla ilgili temel mantığı ve yardımcı fonksiyonları içeren servis
    public class GameService
    {
        private readonly YourTurnDbContext _context;
        private readonly IHubContext<GameHub> _hubContext;
        private const int HOST_TIMEOUT_SECONDS = 30; // Ana bilgisayarın sinyal gönderme zaman aşımı

        public GameService(YourTurnDbContext context, IHubContext<GameHub> hubContext)
        {
            _context = context;
            _hubContext = hubContext;
        }

        // Bir lobiyi koduna göre bulur
        public Lobby? FindLobby(string code)
        {
            return LobbyStore.ActiveLobbies.FirstOrDefault(l => l.LobbyCode == code);
        }

        // Bir lobiyi koduna göre (büyük/küçük harf duyarsız) bulur
        public Lobby? FindLobbyIgnoreCase(string code)
        {
            return LobbyStore.ActiveLobbies.FirstOrDefault(l => l.LobbyCode.Equals(code, StringComparison.OrdinalIgnoreCase));
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
            foreach (var lobby in LobbyStore.ActiveLobbies.Where(l => l.IsPeerHosted))
            {
                lobby.IsHostOnline = await IsPeerHostOnlineAsync(lobby);
            }
        }

        // Belirtilen kategoriden rastgele bir soru döndürür
        public async Task<Question?> GetRandomQuestionAsync(string categoryName, List<int>? excludeQuestionIds = null)
        {
            var category = await _context.Categories
                .Include(c => c.Questions)
                .ThenInclude(q => q.Answers)
                .FirstOrDefaultAsync(c => c.Name == categoryName);

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
            var setting = _context.AdminSettings.FirstOrDefault(s => s.SettingKey == "WinningScore");
            return setting != null && int.TryParse(setting.SettingValue, out var value) ? value : 5;
        }

        // Dinamik olarak timer hızını getirir
        public double GetTimerSpeed()
        {
            var setting = _context.AdminSettings.FirstOrDefault(s => s.SettingKey == "TimerSpeed");
            return setting != null && double.TryParse(setting.SettingValue, out var value) ? value : 0.2;
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
                return "Sol";
            if (team2Score >= winningScore)
                return "Sağ";
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

            var team1Count = lobby.Players.Count(p => p.Team == "Sol");
            var team2Count = lobby.Players.Count(p => p.Team == "Sağ");

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
                    // Kırmızı takım doğru cevap verdi, fitil maviye doğru ilerler.
                    lobby.GameState.FusePosition += 25; 
                }
                else
                {
                    // Mavi takım doğru cevap verdi, fitil kırmızıya doğru ilerler.
                    lobby.GameState.FusePosition -= 25;
                }
                lobby.GameState.FusePosition = Math.Clamp(lobby.GameState.FusePosition, -100, 100);


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
                        kazananTakim = "Sağ";
                        lobby.GameState.Team2Score++;
                    } else { // Sağ'a daha yakınsa veya ortadaysa, Sol kazanır
                        kazananTakim = "Sol";
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
    }
} 