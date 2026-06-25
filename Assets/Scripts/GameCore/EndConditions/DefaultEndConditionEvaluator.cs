using GameCore.Sim;

namespace GameCore.EndConditions
{
    public class DefaultEndConditionEvaluator : IEndConditionEvaluator
    {
        public EndResult Evaluate(WorldState state, SimConfig config)
        {
            // Win first — reaching wealth threshold is a clean exit
            if (config.EnableWealthWin && state.Purse >= config.WealthWinThreshold)
                return EndResult.GameOver(EndReason.WealthWin);

            // Town collapse fail — §6.4
            if (config.EnableBankruptcyFail && state.TownQuality <= config.TownCollapseQualityThreshold)
                return EndResult.GameOver(EndReason.BankruptcyCollapse);

            return EndResult.Continue;
        }
    }
}
