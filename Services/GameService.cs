using YourTurn.Web.Models;
using YourTurn.Web.Stores;

namespace YourTurn.Web.Services
{
    public static class GameService
    {
        private const int WINNING_SCORE = 5;
        private const int HOST_TIMEOUT_SECONDS = 30; // Host timeout for heartbeat

        /// <summary>
        /// Finds a lobby by its code
        /// </summary>
        public static Lobby? FindLobby(string code)
        {
            return LobbyStore.ActiveLobbies.FirstOrDefault(l => l.LobbyCode == code);
        }

        /// <summary>
        /// Finds a lobby by its code with case-insensitive comparison
        /// </summary>
        public static Lobby? FindLobbyIgnoreCase(string code)
        {
            return LobbyStore.ActiveLobbies.FirstOrDefault(l => l.LobbyCode.Equals(code, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Creates a new player with the given name
        /// </summary>
        public static YourTurn.Web.Models.Player CreatePlayer(string playerName)
        {
            return new YourTurn.Web.Models.Player
            {
                Name = playerName,
                Team = ""
            };
        }

        /// <summary>
        /// Checks if a peer host is still online based on heartbeat
        /// </summary>
        public static bool IsPeerHostOnline(Lobby lobby)
        {
            if (!lobby.IsPeerHosted || lobby.LastHostHeartbeat == null)
                return false;

            var timeSinceLastHeartbeat = DateTime.Now - lobby.LastHostHeartbeat.Value;
            return timeSinceLastHeartbeat.TotalSeconds < HOST_TIMEOUT_SECONDS;
        }

        /// <summary>
        /// Updates host online status for all lobbies
        /// </summary>
        public static void UpdateHostStatuses()
        {
            foreach (var lobby in LobbyStore.ActiveLobbies.Where(l => l.IsPeerHosted))
            {
                lobby.IsHostOnline = IsPeerHostOnline(lobby);
            }
        }

        /// <summary>
        /// Gets the next available player to become host when current host goes offline
        /// </summary>
        public static string? GetNextHost(Lobby lobby)
        {
            var currentHostIndex = lobby.Players.FindIndex(p => p.Name == lobby.HostPlayerName);
            if (currentHostIndex == -1 || lobby.Players.Count <= 1)
                return null;

            var nextIndex = (currentHostIndex + 1) % lobby.Players.Count;
            return lobby.Players[nextIndex].Name;
        }

        /// <summary>
        /// Returns a random question from the specified category
        /// </summary>
        public static string GetRandomQuestion(string category)
        {
            var questions = new Dictionary<string, List<string>>
            {
                ["Genel Kültür"] = new List<string>
                {
                    "Türkiye'nin başkenti neresidir?",
                    "Dünyanın en büyük okyanusu hangisidir?",
                    "Einstein'ın ünlü formülü nedir?",
                    "Mona Lisa tablosunu kim yapmıştır?",
                    "Ay'a ilk ayak basan kişi kimdir?"
                },
                ["Spor"] = new List<string>
                {
                    "Dünya Kupası kaç yılda bir düzenlenir?",
                    "Basketbolda kaç oyuncu sahada bulunur?",
                    "Olimpiyat oyunlarının sembolü nedir?",
                    "Fenerbahçe'nin kurulduğu yıl?",
                    "Tenis kortunda kaç set oynanır?"
                },
                ["Tarih"] = new List<string>
                {
                    "Osmanlı İmparatorluğu hangi yılda kuruldu?",
                    "İstanbul'un fethi hangi yılda gerçekleşti?",
                    "Atatürk hangi yılında doğdu?",
                    "I. Dünya Savaşı hangi yıllar arasında yaşandı?",
                    "Cumhuriyet hangi yılda ilan edildi?"
                },
                ["Bilim"] = new List<string>
                {
                    "Suyun kimyasal formülü nedir?",
                    "Güneş sistemindeki en büyük gezegen hangisidir?",
                    "İnsan vücudundaki en büyük organ hangisidir?",
                    "DNA'nın açılımı nedir?",
                    "Periyodik tabloda kaç element vardır?"
                },
                ["Sanat"] = new List<string>
                {
                    "Van Gogh'un en ünlü tablosu hangisidir?",
                    "Mozart hangi ülkede doğmuştur?",
                    "Sistine Şapeli'nin tavanını kim boyamıştır?",
                    "Hamlet'in yazarı kimdir?",
                    "La Gioconda'nın diğer adı nedir?"
                },
                ["Coğrafya"] = new List<string>
                {
                    "Dünyanın en yüksek dağı hangisidir?",
                    "Amazon Nehri hangi kıtada bulunur?",
                    "Türkiye'nin en büyük gölü hangisidir?",
                    "Sahra Çölü hangi kıtada bulunur?",
                    "Dünyanın en büyük kıtası hangisidir?"
                },
                ["Eğlence"] = new List<string>
                {
                    "Mickey Mouse'u kim yaratmıştır?",
                    "Star Wars serisinin ilk filmi hangi yılda çıkmıştır?",
                    "Harry Potter serisinin yazarı kimdir?",
                    "Superman'in gerçek adı nedir?",
                    "Pikachu hangi oyun serisinden gelir?"
                },
                ["Teknoloji"] = new List<string>
                {
                    "İlk iPhone hangi yılda çıkmıştır?",
                    "Google'ın kurucuları kimlerdir?",
                    "Windows işletim sistemini kim geliştirmiştir?",
                    "İnternetin mucidi kimdir?",
                    "Apple'ın kurucusu kimdir?"
                }
            };

            if (questions.ContainsKey(category))
            {
                var categoryQuestions = questions[category];
                var random = new Random();
                return categoryQuestions[random.Next(categoryQuestions.Count)];
            }

            return "Bu kategoride henüz soru bulunmamaktadır.";
        }

        /// <summary>
        /// Initializes a new game state for a lobby
        /// </summary>
        public static GameState InitializeGameState(string category)
        {
            return new GameState
            {
                FusePosition = 0,
                GameStartTime = DateTime.Now,
                LastTurnStartTime = DateTime.Now,
                CurrentQuestion = GetRandomQuestion(category),
                Team1Score = 0,
                Team2Score = 0,
                IsGameActive = true,
                IsTimerRunning = false,
                TimerSpeed = 0.2,
                IsWaitingForVolunteers = true
            };
        }

        /// <summary>
        /// Initializes a new round while preserving scores
        /// </summary>
        public static GameState InitializeNewRound(string category, int team1Score, int team2Score, string team1Volunteer, string team2Volunteer)
        {
            // Randomly select which team starts
            var random = new Random();
            var team1Starts = random.Next(2) == 0; // 50% chance for each team
            
            return new GameState
            {
                FusePosition = 0,
                GameStartTime = DateTime.Now,
                LastTurnStartTime = DateTime.Now,
                CurrentQuestion = GetRandomQuestion(category),
                Team1Score = team1Score,
                Team2Score = team2Score,
                IsGameActive = true,
                IsTimerRunning = false,
                TimerSpeed = 0.2,
                IsWaitingForVolunteers = false,
                Team1Volunteer = team1Volunteer,
                Team2Volunteer = team2Volunteer,
                ActivePlayer1 = team1Volunteer,
                ActivePlayer2 = team2Volunteer,
                CurrentTurn = team1Starts ? team1Volunteer : team2Volunteer
            };
        }

        /// <summary>
        /// Checks if a team has reached the winning score
        /// </summary>
        public static bool HasWinningTeam(int team1Score, int team2Score)
        {
            return team1Score >= WINNING_SCORE || team2Score >= WINNING_SCORE;
        }

        /// <summary>
        /// Gets the winning team name
        /// </summary>
        public static string? GetWinningTeam(int team1Score, int team2Score)
        {
            if (team1Score >= WINNING_SCORE)
                return "Sol";
            if (team2Score >= WINNING_SCORE)
                return "Sağ";
            return null;
        }

        /// <summary>
        /// Generates a random player name if none is provided
        /// </summary>
        public static string GeneratePlayerName(string? playerName = null)
        {
            return playerName ?? "Oyuncu" + new Random().Next(1000, 9999);
        }

        /// <summary>
        /// Validates if a lobby can start a game
        /// </summary>
        public static (bool canStart, string? errorMessage) ValidateGameStart(Lobby lobby)
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