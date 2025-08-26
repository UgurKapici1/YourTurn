namespace YourTurn.Web.Models
{
    public static class GameConstants
    {
        public const string TeamLeft = "Sol";
        public const string TeamRight = "Sağ";

        public const double FuseMin = -100;
        public const double FuseMax = 100;
        public const double CorrectAnswerStep = 25; // amount to move fuse on correct answer
        public const int HostTimeoutSeconds = 30;
    }
}

