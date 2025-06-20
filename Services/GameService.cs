using YourTurn.Web.Models;
using YourTurn.Web.Stores;

namespace YourTurn.Web.Services
{
    // Oyunla ilgili temel mantığı ve yardımcı fonksiyonları içeren servis
    public static class GameService
    {
        private const int WINNING_SCORE = 5;
        private const int HOST_TIMEOUT_SECONDS = 30; // Ana bilgisayarın sinyal gönderme zaman aşımı

        // Bir lobiyi koduna göre bulur
        public static Lobby? FindLobby(string code)
        {
            return LobbyStore.ActiveLobbies.FirstOrDefault(l => l.LobbyCode == code);
        }

        // Bir lobiyi koduna göre (büyük/küçük harf duyarsız) bulur
        public static Lobby? FindLobbyIgnoreCase(string code)
        {
            return LobbyStore.ActiveLobbies.FirstOrDefault(l => l.LobbyCode.Equals(code, StringComparison.OrdinalIgnoreCase));
        }

        // Verilen isimle yeni bir oyuncu oluşturur
        public static YourTurn.Web.Models.Player CreatePlayer(string playerName)
        {
            return new YourTurn.Web.Models.Player
            {
                Name = playerName,
                Team = ""
            };
        }

        // Bir eş ana bilgisayarın hala çevrimiçi olup olmadığını sinyaline göre kontrol eder
        public static bool IsPeerHostOnline(Lobby lobby)
        {
            if (!lobby.IsPeerHosted || lobby.LastHostHeartbeat == null)
                return false;

            var timeSinceLastHeartbeat = DateTime.Now - lobby.LastHostHeartbeat.Value;
            return timeSinceLastHeartbeat.TotalSeconds < HOST_TIMEOUT_SECONDS;
        }

        // Tüm lobiler için ana bilgisayar çevrimiçi durumunu günceller
        public static void UpdateHostStatuses()
        {
            foreach (var lobby in LobbyStore.ActiveLobbies.Where(l => l.IsPeerHosted))
            {
                lobby.IsHostOnline = IsPeerHostOnline(lobby);
            }
        }

        // Belirtilen kategoriden rastgele bir soru döndürür
        public static QuestionAnswer GetRandomQuestion(string category)
        {
            var questions = new Dictionary<string, List<QuestionAnswer>>
            {
                ["Genel Kültür"] = new List<QuestionAnswer>
                {
                    new QuestionAnswer { Question = "Türkiye'nin başkenti neresidir?", Answer = "Ankara" },
                    new QuestionAnswer { Question = "Dünyanın en büyük okyanusu hangisidir?", Answer = "Pasifik Okyanusu" },
                    new QuestionAnswer { Question = "Einstein'ın ünlü formülü nedir?", Answer = "E=mc^2" },
                    new QuestionAnswer { Question = "Mona Lisa tablosunu kim yapmıştır?", Answer = "Leonardo da Vinci" },
                    new QuestionAnswer { Question = "Ay'a ilk ayak basan kişi kimdir?", Answer = "Neil Armstrong" }
                },
                ["Spor"] = new List<QuestionAnswer>
                {
                    new QuestionAnswer { Question = "Dünya Kupası kaç yılda bir düzenlenir?", Answer = "4" },
                    new QuestionAnswer { Question = "Basketbolda kaç oyuncu sahada bulunur?", Answer = "10" },
                    new QuestionAnswer { Question = "Olimpiyat oyunlarının sembolü nedir?", Answer = "Beş halka" },
                    new QuestionAnswer { Question = "Fenerbahçe'nin kurulduğu yıl?", Answer = "1907" },
                    new QuestionAnswer { Question = "Tenis kortunda kaç set oynanır?", Answer = "3 veya 5" }
                },
                ["Tarih"] = new List<QuestionAnswer>
                {
                    new QuestionAnswer { Question = "Osmanlı İmparatorluğu hangi yılda kuruldu?", Answer = "1299" },
                    new QuestionAnswer { Question = "İstanbul'un fethi hangi yılda gerçekleşti?", Answer = "1453" },
                    new QuestionAnswer { Question = "Atatürk hangi yılında doğdu?", Answer = "1881" },
                    new QuestionAnswer { Question = "I. Dünya Savaşı hangi yıllar arasında yaşandı?", Answer = "1914-1918" },
                    new QuestionAnswer { Question = "Cumhuriyet hangi yılda ilan edildi?", Answer = "1923" }
                },
                ["Bilim"] = new List<QuestionAnswer>
                {
                    new QuestionAnswer { Question = "Suyun kimyasal formülü nedir?", Answer = "H2O" },
                    new QuestionAnswer { Question = "Güneş sistemindeki en büyük gezegen hangisidir?", Answer = "Jüpiter" },
                    new QuestionAnswer { Question = "İnsan vücudundaki en büyük organ hangisidir?", Answer = "Deri" },
                    new QuestionAnswer { Question = "DNA'nın açılımı nedir?", Answer = "Deoksiribonükleik asit" },
                    new QuestionAnswer { Question = "Periyodik tabloda kaç element vardır?", Answer = "118" }
                },
                ["Sanat"] = new List<QuestionAnswer>
                {
                    new QuestionAnswer { Question = "Van Gogh'un en ünlü tablosu hangisidir?", Answer = "Yıldızlı Gece" },
                    new QuestionAnswer { Question = "Mozart hangi ülkede doğmuştur?", Answer = "Avusturya" },
                    new QuestionAnswer { Question = "Sistine Şapeli'nin tavanını kim boyamıştır?", Answer = "Michelangelo" },
                    new QuestionAnswer { Question = "Hamlet'in yazarı kimdir?", Answer = "William Shakespeare" },
                    new QuestionAnswer { Question = "La Gioconda'nın diğer adı nedir?", Answer = "Mona Lisa" }
                },
                ["Coğrafya"] = new List<QuestionAnswer>
                {
                    new QuestionAnswer { Question = "Dünyanın en yüksek dağı hangisidir?", Answer = "Everest" },
                    new QuestionAnswer { Question = "Amazon Nehri hangi kıtada bulunur?", Answer = "Güney Amerika" },
                    new QuestionAnswer { Question = "Türkiye'nin en büyük gölü hangisidir?", Answer = "Van Gölü" },
                    new QuestionAnswer { Question = "Sahra Çölü hangi kıtada bulunur?", Answer = "Afrika" },
                    new QuestionAnswer { Question = "Dünyanın en büyük kıtası hangisidir?", Answer = "Asya" }
                },
                ["Eğlence"] = new List<QuestionAnswer>
                {
                    new QuestionAnswer { Question = "Mickey Mouse'u kim yaratmıştır?", Answer = "Walt Disney" },
                    new QuestionAnswer { Question = "Star Wars serisinin ilk filmi hangi yılda çıkmıştır?", Answer = "1977" },
                    new QuestionAnswer { Question = "Harry Potter serisinin yazarı kimdir?", Answer = "J.K. Rowling" },
                    new QuestionAnswer { Question = "Superman'in gerçek adı nedir?", Answer = "Clark Kent" },
                    new QuestionAnswer { Question = "Pikachu hangi oyun serisinden gelir?", Answer = "Pokemon" }
                },
                ["Teknoloji"] = new List<QuestionAnswer>
                {
                    new QuestionAnswer { Question = "İlk iPhone hangi yılda çıkmıştır?", Answer = "2007" },
                    new QuestionAnswer { Question = "Google'ın kurucuları kimlerdir?", Answer = "Larry Page ve Sergey Brin" },
                    new QuestionAnswer { Question = "Windows işletim sistemini kim geliştirmiştir?", Answer = "Bill Gates" },
                    new QuestionAnswer { Question = "İnternetin mucidi kimdir?", Answer = "Tim Berners-Lee" },
                    new QuestionAnswer { Question = "Apple'ın kurucusu kimdir?", Answer = "Steve Jobs" }
                }
            };

            if (questions.ContainsKey(category))
            {
                var categoryQuestions = questions[category];
                var random = new Random();
                return categoryQuestions[random.Next(categoryQuestions.Count)];
            }

            return new QuestionAnswer { Question = "Bu kategoride henüz soru bulunmamaktadır.", Answer = "" };
        }

        // Bir lobi için yeni bir oyun durumu başlatır
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

        // Skorları koruyarak yeni bir tur başlatır
        public static GameState InitializeNewRound(string category, int team1Score, int team2Score, string team1Volunteer, string team2Volunteer)
        {
            // Hangi takımın başlayacağını rastgele seç
            var random = new Random();
            var team1Starts = random.Next(2) == 0; // Her takım için %50 şans
            
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

        // Bir takımın kazanma skoruna ulaşıp ulaşmadığını kontrol eder
        public static bool HasWinningTeam(int team1Score, int team2Score)
        {
            return team1Score >= WINNING_SCORE || team2Score >= WINNING_SCORE;
        }

        // Kazanan takımın adını alır
        public static string? GetWinningTeam(int team1Score, int team2Score)
        {
            if (team1Score >= WINNING_SCORE)
                return "Sol";
            if (team2Score >= WINNING_SCORE)
                return "Sağ";
            return null;
        }

        // Oyuncu adı sağlanmamışsa rastgele bir oyuncu adı oluşturur
        public static string GeneratePlayerName(string? playerName = null)
        {
            return playerName ?? "Oyuncu" + new Random().Next(1000, 9999);
        }

        // Bir lobinin oyuna başlayıp başlayamayacağını doğrular
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