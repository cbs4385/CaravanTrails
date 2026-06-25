using System;
using GameCore.Sim;

namespace GameCore.Heat
{
    public class DefaultHeatModel : IHeatModel
    {
        public float ComputeAccrual(WorldState state, PlayerInput input, TickContext ctx, SimConfig config)
        {
            // Skim gap: the auditor compares expected vs deposited revenue
            float skimHeat = ctx.SkimmedAmount * config.HeatAccrualPerSkimUnit;

            // Crime: organized generates more Heat per unit than unorganized (§6.3)
            float unorganizedCrimeHeat = ctx.UnorganizedCrimeYield * config.UnorganizedCrimeHeatPerYieldUnit;
            float organizedCrimeHeat = state.OrganizedCrimeLevel * config.OrganizedCrimeHeatPerLevel;

            // Legitimacy buffer: visible coffers spending offsets some accrual
            float buffer = ctx.CoffersContribution * config.LegitimacyHeatBufferPerCoffersUnit;

            float accrual = skimHeat + unorganizedCrimeHeat + organizedCrimeHeat - buffer;
            return accrual < 0f ? 0f : accrual;
        }

        public float ComputeNaturalDecay(WorldState state, SimConfig config)
        {
            float upgradeBonus = state.HeatDecayUpgradeLevel * config.UpgradeHeatDecayBonusPerLevel;
            return config.HeatNaturalDecayPerTick + upgradeBonus;
        }

        public AuditResult CheckAudit(WorldState state, SimConfig config, Random rng)
        {
            if (!config.EnableAuditFail || state.Heat <= config.AuditThreshold)
                return AuditResult.NoAudit;

            float excessHeat = state.Heat - config.AuditThreshold;
            float auditChance = excessHeat * config.AuditChancePerHeatPointAboveThreshold;

            if (rng.NextDouble() >= auditChance)
                return AuditResult.NoAudit;

            // Audit fires — fine is proportional to purse (what can be seized)
            float fine = state.Purse * config.AuditFineMultiplier;
            return new AuditResult
            {
                AuditOccurred = true,
                AuditFailed = true,
                FineAmount = fine,
            };
        }
    }
}
