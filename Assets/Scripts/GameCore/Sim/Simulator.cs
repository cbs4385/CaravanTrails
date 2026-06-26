using System;
using System.Collections.Generic;
using GameCore.Crime;
using GameCore.Economy;
using GameCore.EndConditions;
using GameCore.Events;
using GameCore.Heat;
using GameCore.Sources;

namespace GameCore.Sim
{
    // §8.2: deterministic, seeded, Unity-free. Advance by calling Tick(input) explicitly.
    public class Simulator
    {
        public WorldState State { get; private set; }
        public SimConfig Config { get; }

        private readonly Random _rng;
        private readonly ICaravanSource _caravanSource;
        private readonly IHeatModel _heatModel;
        private readonly ICrimeModel _crimeModel;
        private readonly IEndConditionEvaluator _endCondition;
        private readonly IEventModel _eventModel;

        private readonly List<TelemetryRecord> _telemetry = new List<TelemetryRecord>();
        public IReadOnlyList<TelemetryRecord> Telemetry => _telemetry;

        public Simulator(
            SimConfig config,
            int seed,
            ICaravanSource caravanSource = null,
            IHeatModel heatModel = null,
            ICrimeModel crimeModel = null,
            IEndConditionEvaluator endCondition = null,
            IEventModel eventModel = null,
            WorldState initialState = null)
        {
            Config = config;
            _rng = new Random(seed);
            _caravanSource = caravanSource ?? new AggregateCaravanSource();
            _heatModel = heatModel ?? new DefaultHeatModel();
            _crimeModel = crimeModel ?? new DefaultCrimeModel();
            _endCondition = endCondition ?? new DefaultEndConditionEvaluator();
            _eventModel = eventModel ?? new DefaultEventModel();
            State = (initialState ?? WorldState.Default()).Clone();
        }

        public TickResult Tick(PlayerInput input)
        {
            if (State.IsGameOver)
                throw new InvalidOperationException("Tick called on a finished simulation.");

            var s = State.Clone();
            var ctx = new TickContext();

            // 0. Resolve pending event from previous tick (None choice = auto-dismiss)
            _eventModel.ResolveEvent(s, input.EventChoice, Config);

            // 1. Route attractiveness & traffic (RouteImprovement adds flat attractiveness bonus)
            ctx.RouteAttractiveness = TrafficModel.ComputeAttractiveness(s, input.TaxRate, Config)
                + s.RouteImprovementLevel * Config.UpgradeRouteImprovementAttractivenessPerLevel;
            ctx.TrafficVolume = _caravanSource.GetTrafficVolume(ctx.RouteAttractiveness, Config, _rng);

            // 2. Official revenue & skim (§4.2)
            ctx.OfficialRevenue = TaxationModel.ComputeOfficialRevenue(ctx.TrafficVolume, input.TaxRate);
            (ctx.SkimmedAmount, ctx.CoffersContribution) =
                TaxationModel.ApplySkim(ctx.OfficialRevenue, input.SkimFraction, s.CollectionUpgradeLevel, Config);

            s.Purse += ctx.SkimmedAmount;
            s.Coffers += ctx.CoffersContribution;

            // 3. Crime (§4.4)
            var crimeResult = _crimeModel.Simulate(s, input, ctx.TrafficVolume, Config, _rng);
            ctx.CrimeYield = crimeResult.TotalYield;
            ctx.UnorganizedCrimeYield = crimeResult.UnorganizedYield;
            ctx.RivalPressure = crimeResult.RivalPressure;
            ctx.BetrayalOccurred = crimeResult.BetrayalOccurred;
            s.Purse += crimeResult.TotalYield;

            // 4. Organized crime upkeep — if can't pay, shed a level
            float upkeep = _crimeModel.GetOrganizedUpkeep(s.OrganizedCrimeLevel, Config);
            if (s.Purse >= upkeep)
            {
                s.Purse -= upkeep;
            }
            else
            {
                s.OrganizedCrimeLevel = Math.Max(0, s.OrganizedCrimeLevel - 1);
            }

            // 5. Organized crime investment (§4.4)
            if (input.OrganizedCrimeLevelDelta > 0)
            {
                float setupCost = _crimeModel.GetOrganizedSetupCost(
                    input.OrganizedCrimeLevelDelta, s.OrganizedCrimeLevel, Config);
                if (s.Purse >= setupCost)
                {
                    s.Purse -= setupCost;
                    s.OrganizedCrimeLevel += input.OrganizedCrimeLevelDelta;
                }
                // else: insufficient funds; silently skip (host should validate before calling)
            }

            // 6. Tribute & town quality (§4.3; TownInvestment adds flat quality per tick)
            CoffersModel.ApplyTribute(ref s.Coffers, ref s.Heat, Config);
            s.TownQuality = CoffersModel.UpdateTownQuality(s.TownQuality, s.Coffers, Config);
            s.TownQuality = Clamp01(s.TownQuality
                + s.TownInvestmentLevel * Config.UpgradeTownInvestmentQualityBonusPerLevel);

            // 7. Safety — coffers-funded recovery, then organized crime penalty (§4.4)
            s.Safety = CoffersModel.UpdateSafety(s.Safety, s.Coffers, Config);
            s.Safety = Clamp01(s.Safety + crimeResult.SafetyDelta);

            // 8. Reputation — lagged perception of predation level (§4.1)
            float predation = input.TaxRate * Config.ReputationTaxWeight
                            + (s.Heat / 100f) * Config.ReputationHeatWeight;
            float targetReputation = s.TownQuality * Config.ReputationTownWeight
                                   + s.Safety * Config.ReputationSafetyWeight
                                   - predation;
            s.Reputation = Lerp(s.Reputation, Clamp01(targetReputation), Config.ReputationLagFactor);

            // 9. Heat accrual (§4.5; Connections reduces accrual by 10% per level)
            float heatAccrual = _heatModel.ComputeAccrual(s, input, ctx, Config)
                * Math.Max(0f, 1f - s.ConnectionsLevel * Config.UpgradeConnectionsHeatReductionPerLevel);
            float heatDecay = _heatModel.ComputeNaturalDecay(s, Config);

            // Bribe: pay what the player can afford, get proportional heat reduction
            float effectiveBribe = Math.Min(input.BribeAmount, s.Purse);
            if (effectiveBribe > 0f)
                s.Purse -= effectiveBribe;
            float bribeDecay = effectiveBribe * Config.HeatDecayPerBribeUnit;

            s.Heat = Math.Max(0f, s.Heat + heatAccrual - heatDecay - bribeDecay);

            // 10. Audit check (§4.5) — uses heat AFTER accrual/decay
            var auditResult = _heatModel.CheckAudit(s, Config, _rng);
            ctx.AuditOccurred = auditResult.AuditOccurred;
            ctx.AuditFailed = auditResult.AuditFailed;
            ctx.AuditFineAmount = auditResult.FineAmount;

            // 11. Upgrades (§4.6) — purchased from purse this tick
            if (input.Upgrade == UpgradePurchase.Collection)
            {
                float cost = Config.UpgradeCollectionCostBase
                    * (float)Math.Pow(Config.UpgradeCollectionCostScalePerLevel, s.CollectionUpgradeLevel);
                if (s.Purse >= cost)
                {
                    s.Purse -= cost;
                    s.CollectionUpgradeLevel++;
                }
            }
            else if (input.Upgrade == UpgradePurchase.HeatDecay)
            {
                float cost = Config.UpgradeHeatDecayCostBase
                    * (float)Math.Pow(Config.UpgradeHeatDecayCostScalePerLevel, s.HeatDecayUpgradeLevel);
                if (s.Purse >= cost)
                {
                    s.Purse -= cost;
                    s.HeatDecayUpgradeLevel++;
                }
            }
            else if (input.Upgrade == UpgradePurchase.Connections)
            {
                float cost = Config.UpgradeConnectionsCostBase
                    * (float)Math.Pow(Config.UpgradeConnectionsCostScalePerLevel, s.ConnectionsLevel);
                if (s.Purse >= cost)
                {
                    s.Purse -= cost;
                    s.ConnectionsLevel++;
                }
            }
            else if (input.Upgrade == UpgradePurchase.TownInvestment)
            {
                float cost = Config.UpgradeTownInvestmentCostBase
                    * (float)Math.Pow(Config.UpgradeTownInvestmentCostScalePerLevel, s.TownInvestmentLevel);
                if (s.Coffers >= cost)
                {
                    s.Coffers -= cost;
                    s.TownInvestmentLevel++;
                }
            }
            else if (input.Upgrade == UpgradePurchase.RouteImprovement)
            {
                float cost = Config.UpgradeRouteImprovementCostBase
                    * (float)Math.Pow(Config.UpgradeRouteImprovementCostScalePerLevel, s.RouteImprovementLevel);
                if (s.Coffers >= cost)
                {
                    s.Coffers -= cost;
                    s.RouteImprovementLevel++;
                }
            }

            // 12. Resolve end conditions — audit and betrayal override IEndConditionEvaluator (§6.4)
            if (auditResult.AuditFailed)
            {
                s.Purse = Math.Max(0f, s.Purse - auditResult.FineAmount);
                s.IsGameOver = true;
                s.EndReason = EndReason.AuditArrest;
            }
            else if (crimeResult.BetrayalOccurred)
            {
                s.IsGameOver = true;
                s.EndReason = EndReason.RivalOverthrow;
            }
            else
            {
                var endResult = _endCondition.Evaluate(s, Config);
                if (endResult.IsGameOver)
                {
                    s.IsGameOver = true;
                    s.EndReason = endResult.Reason;
                }
            }

            // 13. Fire new event — only when game is still live (§3)
            if (!s.IsGameOver)
            {
                var newEvent = _eventModel.TryFireEvent(s, Config, _rng);
                s.PendingEvent = newEvent;
                ctx.EventFired = newEvent?.Type ?? EventType.None;
            }

            s.Tick++;
            State = s;

            var telemetry = BuildTelemetry(s, input, ctx);
            _telemetry.Add(telemetry);

            return new TickResult
            {
                StateAfter = s.Clone(),
                Telemetry = telemetry,
                AuditOccurred = ctx.AuditOccurred,
                BetrayalOccurred = ctx.BetrayalOccurred,
            };
        }

        private TelemetryRecord BuildTelemetry(WorldState s, PlayerInput input, TickContext ctx) =>
            new TelemetryRecord
            {
                Tick = s.Tick,
                Purse = s.Purse,
                Coffers = s.Coffers,
                TownQuality = s.TownQuality,
                Safety = s.Safety,
                Reputation = s.Reputation,
                Heat = s.Heat,
                TaxRate = input.TaxRate,
                SkimFraction = input.SkimFraction,
                TrafficVolume = ctx.TrafficVolume,
                OfficialRevenue = ctx.OfficialRevenue,
                SkimmedAmount = ctx.SkimmedAmount,
                CoffersContribution = ctx.CoffersContribution,
                CrimeYield = ctx.CrimeYield,
                OrganizedCrimeLevel = s.OrganizedCrimeLevel,
                RivalPressure = ctx.RivalPressure,
                RouteAttractiveness = ctx.RouteAttractiveness,
                AuditFired = ctx.AuditOccurred,
                BetrayalFired = ctx.BetrayalOccurred,
                EndReason = s.EndReason,
                EventFired = ctx.EventFired,
            };

        private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
        private static float Lerp(float a, float b, float t) => a + (b - a) * t;
    }
}
