namespace YourTurn.Web.Models
{
    using YourTurn.Web.Models;

    // Oyunun mevcut durumunu temsil eden model
    public class GameState
    {
        // Takım 1'in aktif oyuncusu
        public string ActivePlayer1 { get; set; }
        // Takım 2'nin aktif oyuncusu
        public string ActivePlayer2 { get; set; }
        // Sıranın kimde olduğunu belirtir
        public string CurrentTurn { get; set; }
        // Zamanlayıcı fitilinin pozisyonu (-100 ile 100 arasında)
        public double FusePosition { get; set; } = 0;
        // Oyunun başlama zamanı
        public DateTime GameStartTime { get; set; }
        // Son turun başlama zamanı
        public DateTime? LastTurnStartTime { get; set; }
        // Mevcut soru ve cevap
        public QuestionAnswer CurrentQuestion { get; set; }
        // Takım 1'in skoru
        public int Team1Score { get; set; }
        // Takım 2'nin skoru
        public int Team2Score { get; set; }
        // Oyunun aktif olup olmadığını belirtir
        public bool IsGameActive { get; set; }
        // Turun kazananı
        public string Winner { get; set; }
        // Son cevap verilme zamanı
        public DateTime? LastAnswerTime { get; set; }
        // Zamanlayıcının çalışıp çalışmadığını belirtir
        public bool IsTimerRunning { get; set; } = false;
        // Zamanlayıcının hızı
        public double TimerSpeed { get; set; } = 2.0;
        // Takım 1'in gönüllüsü
        public string Team1Volunteer { get; set; }
        // Takım 2'nin gönüllüsü
        public string Team2Volunteer { get; set; }
        // Gönüllülerin beklenip beklenmediğini belirtir
        public bool IsWaitingForVolunteers { get; set; } = true;
        // Takım 1 gönüllüsünün cevabının doğrulanıp doğrulanmadığını belirtir
        public bool IsTeam1VolunteerAnswerValidated { get; set; } = false;
        // Takım 2 gönüllüsünün cevabının doğrulanıp doğrulanmadığını belirtir
        public bool IsTeam2VolunteerAnswerValidated { get; set; } = false;
    }
}

