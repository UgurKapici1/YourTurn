using YourTurn.Web.Models;
using YourTurn.Web.Models.Dto;

namespace YourTurn.Web.Interfaces
{
    // Oyun servisinin sözleşmesi (SOLID - DIP)
    public interface IGameService
    {
        Lobby? FindLobby(string code);
        Lobby? FindLobbyIgnoreCase(string code);
        Player CreatePlayer(string playerName);
        Task<bool> IsPeerHostOnlineAsync(Lobby lobby);
        Task UpdateHostStatusesAsync();
        Task<Question?> GetRandomQuestionAsync(string categoryName, List<int>? excludeQuestionIds = null);
        int GetWinningScore();
        double GetTimerSpeed();
        Task<GameState?> InitializeGameStateAsync(string category);
        Task<GameState?> InitializeNewRoundAsync(string category, int team1Score, int team2Score, string team1Volunteer, string team2Volunteer);
        bool HasWinningTeam(int team1Score, int team2Score);
        string? GetWinningTeam(int team1Score, int team2Score);
        string GeneratePlayerName(string? playerName = null);
        (bool canStart, string? errorMessage) ValidateGameStart(Lobby lobby);
        Task<object> SubmitAnswerAsync(string lobbyCode, string playerName, string answer);
        Task ResetGameAsync(string code, string hostPlayerName);
        GameStateDto BuildAndAdvanceGameState(Lobby lobby);
        Task<(bool success, string? message)> VolunteerForTeamAsync(Lobby lobby, string currentPlayerName, string team);
        Task<(bool success, string? message)> WithdrawVolunteerAsync(Lobby lobby, string currentPlayerName, string team);
    }
}

