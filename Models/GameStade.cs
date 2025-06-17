namespace YourTurn.Web.Models
{
    public class GameState
    {
        public string ActivePlayer1 { get; set; }
        public string ActivePlayer2 { get; set; }
        public string CurrentTurn { get; set; }
        public double FusePosition { get; set; } = 0;
        public DateTime GameStartTime { get; set; }
        public DateTime? LastTurnStartTime { get; set; }
        public string CurrentQuestion { get; set; }
        public int Team1Score { get; set; }
        public int Team2Score { get; set; }
        public bool IsGameActive { get; set; }
        public string Winner { get; set; }
        public DateTime? LastAnswerTime { get; set; }
        public bool IsTimerRunning { get; set; } = false;
        public double TimerSpeed { get; set; } = 2.0;
        public string Team1Volunteer { get; set; }
        public string Team2Volunteer { get; set; }
        public bool IsWaitingForVolunteers { get; set; } = true;
    }
}

