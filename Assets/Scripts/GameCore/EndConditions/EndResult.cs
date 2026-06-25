using GameCore.Sim;

namespace GameCore.EndConditions
{
    public class EndResult
    {
        public bool IsGameOver;
        public EndReason Reason;

        public static EndResult Continue => new EndResult { IsGameOver = false, Reason = EndReason.None };
        public static EndResult GameOver(EndReason reason) => new EndResult { IsGameOver = true, Reason = reason };
    }
}
