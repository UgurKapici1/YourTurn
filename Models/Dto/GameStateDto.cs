using System.Collections.Generic;

namespace YourTurn.Web.Models.Dto
{
    public class GameStateDto
    {
        public bool Success { get; set; }
        public bool IsWaitingForVolunteers { get; set; }

        public double FusePosition { get; set; }
        public bool IsTimerRunning { get; set; }
        public string? CurrentTurn { get; set; }
        public bool IsGameActive { get; set; }
        public string? Winner { get; set; }

        public int Team1Score { get; set; }
        public int Team2Score { get; set; }
        public string? ActivePlayer1 { get; set; }
        public string? ActivePlayer2 { get; set; }

        public string? GameWinner { get; set; }
        public bool IsGameCompleted { get; set; }

        public string? Question { get; set; }

        public string? Team1Volunteer { get; set; }
        public string? Team2Volunteer { get; set; }

        public List<PlayerDto> Players { get; set; } = new List<PlayerDto>();
    }

    public class PlayerDto
    {
        public string Name { get; set; } = string.Empty;
        public string Team { get; set; } = string.Empty;
    }
}

