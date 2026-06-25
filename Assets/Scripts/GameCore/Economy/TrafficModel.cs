using System;
using GameCore.Sim;

namespace GameCore.Economy
{
    // §4.1: A = f(effective_tax, safety, town_quality, reputation)
    // §6.1: A_tax = exp(-k * tax_rate) — creates a Laffer-style interior optimum
    public static class TrafficModel
    {
        public static float ComputeAttractiveness(WorldState state, float taxRate, SimConfig config)
        {
            float aTax = (float)Math.Exp(-config.TaxElasticityK * taxRate);
            float aSafety = Clamp01(state.Safety);
            float aTownQuality = Clamp01(state.TownQuality);
            float aReputation = Clamp01(state.Reputation);

            return config.AttractivenessWeightTax * aTax
                 + config.AttractivenessWeightSafety * aSafety
                 + config.AttractivenessWeightTownQuality * aTownQuality
                 + config.AttractivenessWeightReputation * aReputation;
        }

        private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }
}
