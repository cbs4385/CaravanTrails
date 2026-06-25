using GameCore.Sim;

namespace GameCore.Economy
{
    // §4.3: coffers fund town quality and mandatory tribute to the capital
    public static class CoffersModel
    {
        // Returns tribute actually paid; spikes heat if coffers insufficient.
        public static float ApplyTribute(ref float coffers, ref float heat, SimConfig config)
        {
            if (coffers >= config.TributePerTick)
            {
                coffers -= config.TributePerTick;
                return config.TributePerTick;
            }

            float paid = coffers;
            coffers = 0f;
            heat += config.MissedTributeHeatSpike;
            return paid;
        }

        // Town quality rises when coffers are healthy, decays when starved.
        public static float UpdateTownQuality(float currentQuality, float coffers, SimConfig config)
        {
            float fundingRatio = coffers / config.TownQualityFullFundingThreshold;
            if (fundingRatio > 1f) fundingRatio = 1f;

            float gain = fundingRatio * config.TownQualityGainRatePerTick;
            float decay = (1f - fundingRatio) * config.TownQualityDecayRatePerTick;
            float next = currentQuality + gain - decay;

            return next < 0f ? 0f : next > 1f ? 1f : next;
        }

        // Safety rises when coffers are healthy, decays when starved — mirrors UpdateTownQuality.
        public static float UpdateSafety(float currentSafety, float coffers, SimConfig config)
        {
            float fundingRatio = coffers / config.SafetyFullFundingThreshold;
            if (fundingRatio > 1f) fundingRatio = 1f;

            float gain = fundingRatio * config.SafetyGainRatePerTick;
            float decay = (1f - fundingRatio) * config.SafetyDecayRatePerTick;
            float next = currentSafety + gain - decay;

            return next < 0f ? 0f : next > 1f ? 1f : next;
        }
    }
}
