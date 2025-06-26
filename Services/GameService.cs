using YourTurn.Web.Models;
using YourTurn.Web.Stores;
using YourTurn.Web.Data;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;

namespace YourTurn.Web.Services
{
    // Oyunla ilgili temel mantığı ve yardımcı fonksiyonları içeren servis
    public class GameService
    {
        private readonly YourTurnDbContext _context;
        private const int HOST_TIMEOUT_SECONDS = 30; // Ana bilgisayarın sinyal gönderme zaman aşımı

        public GameService(YourTurnDbContext context)
        {
            _context = context;
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
    }
} 