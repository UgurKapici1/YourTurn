namespace YourTurn.Web.Interfaces
{
    // Ayarları okumak için servis (SRP, DIP)
    public interface ISettingsService
    {
        int GetWinningScore();
        double GetTimerSpeed();
    }
}

