using System;

namespace GameCore.Sim
{
    // Simple rule-based AI that drives each rival town's tax and quality per tick.
    // Rivals drift toward a personality baseline with small noise, and invest in
    // town quality when their tax is below the funding threshold.
    public static class RivalTownAI
    {
        public static void Tick(RivalTownState[] rivals, SimConfig cfg, Random rng)
        {
            if (rivals == null) return;
            for (int i = 0; i < rivals.Length; i++)
            {
                var r = rivals[i];
                float baseTax = i < cfg.RivalBaseTaxRates.Length ? cfg.RivalBaseTaxRates[i] : 0.15f;

                float noise = (float)(rng.NextDouble() * 2.0 - 1.0) * cfg.RivalTaxDriftMagnitude;
                r.TaxRate = Clamp(r.TaxRate + (baseTax - r.TaxRate) * 0.05f + noise, 0.05f, 0.50f);

                bool investing = r.TaxRate < cfg.RivalQualityFundingThresh;
                r.Quality = Clamp01(r.Quality + (investing ?  cfg.RivalQualityGainRate : -cfg.RivalQualityDecayRate));
                r.Safety  = Clamp01(r.Safety  + (investing ?  cfg.RivalQualityGainRate * 0.5f : -cfg.RivalQualityDecayRate * 0.5f));
            }
        }

        public static RivalTownState[] BuildDefaults(SimConfig cfg)
        {
            int n = cfg.RivalBaseTaxRates.Length;
            var rivals = new RivalTownState[n];
            float initialShare = 1f / (n + 1);
            for (int i = 0; i < n; i++)
                rivals[i] = new RivalTownState
                {
                    TaxRate      = cfg.RivalBaseTaxRates[i],
                    Quality      = i < cfg.RivalInitialQualities.Length ? cfg.RivalInitialQualities[i] : 0.55f,
                    Safety       = i < cfg.RivalInitialSafeties.Length  ? cfg.RivalInitialSafeties[i]  : 0.60f,
                    TrafficShare = initialShare,
                };
            return rivals;
        }

        static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
        static float Clamp(float v, float lo, float hi) => v < lo ? lo : v > hi ? hi : v;
    }
}
