using System;

namespace GameCore.Sim
{
    [Serializable]
    public class SimConfig
    {
        // §6.1 Tax elasticity — A_tax = exp(-TaxElasticityK * tax_rate)
        public float TaxElasticityK = 3.5f;

        // §4.1 Traffic
        public float BaseTrafficVolume = 100f;
        public float TrafficNoiseMagnitude = 0.1f;

        // §4.1 Attractiveness weights
        public float AttractivenessWeightTax = 0.35f;
        public float AttractivenessWeightSafety = 0.30f;
        public float AttractivenessWeightTownQuality = 0.25f;
        public float AttractivenessWeightReputation = 0.10f;

        // §4.2 Taxation & skim — heat accrual from the gap
        public float HeatAccrualPerSkimUnit = 0.3f;

        // §4.3 Coffers — town quality & tribute
        public float TownQualityDecayRatePerTick = 0.02f;
        public float TownQualityGainRatePerTick = 0.05f;
        public float TownQualityFullFundingThreshold = 30f;  // coffers balance to consider "fully funded"
        public float TributePerTick = 7f;
        public float MissedTributeHeatSpike = 8f;

        // §4.3 Legitimacy buffer — visible coffers spending lowers heat accrual rate
        public float LegitimacyHeatBufferPerCoffersUnit = 0.005f;

        // §4.4 Crime — unorganized
        public float UnorganizedCrimeYieldFraction = 0.05f;  // fraction of traffic volume
        public float UnorganizedCrimeHeatPerYieldUnit = 0.05f;

        // §4.4 Crime — organized
        public float OrganizedCrimeSetupCostPerLevel = 40f;
        public float OrganizedCrimeUpkeepPerLevel = 10f;
        public float OrganizedCrimeYieldPerLevel = 30f;
        public float OrganizedCrimeHeatPerLevel = 0.54f;    // per tick, per level
        public float OrganizedCrimeRivalPressurePerLevel = 0.08f;
        public float OrganizedCrimeBetrayalOddsPerLevel = 0.003f;  // per tick per level
        public float SafetyPenaltyPerOrganizedLevel = 0.03f;
        public float SafetyFullFundingThreshold = 20f;
        public float SafetyGainRatePerTick = 0.03f;
        public float SafetyDecayRatePerTick = 0.01f;

        // §4.5 Heat — accrual/decay/audit
        public float HeatNaturalDecayPerTick = 0.03f;
        public float HeatDecayPerBribeUnit = 0.06f;
        public float AuditThreshold = 75f;                          // hidden from player
        public float AuditChancePerHeatPointAboveThreshold = 0.004f;
        public float AuditFineMultiplier = 2f;                      // fine = purse * multiplier

        // §4.6 Upgrades — personal (cost from purse)
        public float UpgradeCollectionCostBase = 60f;
        public float UpgradeCollectionCostScalePerLevel = 1.5f;
        public float UpgradeCollectionSkimBonusPerLevel = 0.08f;    // +8% skim efficiency per level
        public float UpgradeHeatDecayCostBase = 80f;
        public float UpgradeHeatDecayCostScalePerLevel = 1.6f;
        public float UpgradeHeatDecayBonusPerLevel = 0.015f;        // +1.5% extra heat decay per level
        public float UpgradeConnectionsCostBase = 75f;
        public float UpgradeConnectionsCostScalePerLevel = 1.5f;
        public float UpgradeConnectionsHeatReductionPerLevel = 0.10f; // -10% heat accrual per level

        // §4.6 Upgrades — town (cost from coffers)
        public float UpgradeTownInvestmentCostBase = 70f;
        public float UpgradeTownInvestmentCostScalePerLevel = 1.4f;
        public float UpgradeTownInvestmentQualityBonusPerLevel = 0.010f; // +1% TownQuality per tick per level
        public float UpgradeRouteImprovementCostBase = 100f;
        public float UpgradeRouteImprovementCostScalePerLevel = 1.6f;
        public float UpgradeRouteImprovementAttractivenessPerLevel = 0.08f; // +0.08 flat attractiveness per level

        // §4.1 Reputation — lagged merchant perception
        public float ReputationTownWeight = 0.4f;
        public float ReputationSafetyWeight = 0.4f;
        public float ReputationTaxWeight = 0.5f;
        public float ReputationHeatWeight = 0.3f;    // heat normalized to 0-1 by dividing by 100
        public float ReputationLagFactor = 0.30f;

        // §6.4 End conditions (all toggleable per §8.4)
        public bool EnableAuditFail = true;
        public bool EnableBankruptcyFail = true;
        public bool EnableWealthWin = true;
        public float WealthWinThreshold = 1900f;
        public float TownCollapseQualityThreshold = 0.05f;

        // §3 Events — all toggleable; probabilities fire at most once per tick
        public bool  EnableEvents = true;
        public float AuditWarningHeatThreshold             = 55f;
        public float AuditWarningChance                    = 0.15f;
        public float AuditWarningBribeCost                 = 30f;
        public float AuditWarningBribeHeatReduction        = 20f;
        public float AuditWarningIgnoreHeatPenalty         = 12f;
        public float RivalIncursionChance                  = 0.10f;
        public float RivalIncursionTributeCost             = 50f;
        public float RivalIncursionRefuseSafetyPenalty     = 0.08f;
        public float MerchantComplaintRepThreshold         = 0.45f;
        public float MerchantComplaintChance               = 0.12f;
        public float MerchantComplaintCompensationCost     = 40f;
        public float MerchantComplaintCompensationRepBonus = 0.12f;
        public float MerchantComplaintDismissRepPenalty    = 0.10f;
        public float InspectorVisitChance                  = 0.05f;
        public int   InspectorVisitStartTick               = 5;
        public float InspectorGiftCost                     = 60f;
        public float InspectorGiftHeatReduction            = 30f;
        public float InspectorStonewallHeatPenalty         = 20f;
    }
}
