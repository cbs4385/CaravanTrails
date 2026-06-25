using System;
using GameCore.Sim;

namespace GameCore.Crime
{
    public class DefaultCrimeModel : ICrimeModel
    {
        public CrimeResult Simulate(WorldState state, PlayerInput input, float trafficVolume, SimConfig config, Random rng)
        {
            // Unorganized: scales with this tick's actual traffic (which reflects route attractiveness)
            float unorganizedYield = state.TownQuality > 0f
                ? input.UnorganizedCrimeIntensity * trafficVolume * config.UnorganizedCrimeYieldFraction
                : 0f;

            // Organized: scales with investment level, independent of player intensity each tick
            float organizedYield = state.OrganizedCrimeLevel * config.OrganizedCrimeYieldPerLevel;

            // Rival pressure grows with operation size — §6.3 counterweight
            float rivalPressure = state.OrganizedCrimeLevel * config.OrganizedCrimeRivalPressurePerLevel;

            // Betrayal: per-level per-tick probability — §6.3 counterweight
            float betrayalChance = state.OrganizedCrimeLevel * config.OrganizedCrimeBetrayalOddsPerLevel;
            bool betrayal = state.OrganizedCrimeLevel > 0 && rng.NextDouble() < betrayalChance;

            // Safety degrades with organized crime presence
            float safetyDelta = -state.OrganizedCrimeLevel * config.SafetyPenaltyPerOrganizedLevel;

            return new CrimeResult
            {
                TotalYield = unorganizedYield + organizedYield,
                UnorganizedYield = unorganizedYield,
                OrganizedYield = organizedYield,
                RivalPressure = rivalPressure,
                BetrayalOccurred = betrayal,
                SafetyDelta = safetyDelta,
            };
        }

        public float GetOrganizedUpkeep(int level, SimConfig config)
        {
            return level * config.OrganizedCrimeUpkeepPerLevel;
        }

        public float GetOrganizedSetupCost(int levelDelta, int currentLevel, SimConfig config)
        {
            if (levelDelta <= 0) return 0f;
            // Each new level costs the same flat rate; can swap to a scaling formula later
            return levelDelta * config.OrganizedCrimeSetupCostPerLevel;
        }
    }
}
