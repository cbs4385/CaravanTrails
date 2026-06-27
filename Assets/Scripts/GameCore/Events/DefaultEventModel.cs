using System;
using GameCore.Sim;

namespace GameCore.Events
{
    public class DefaultEventModel : IEventModel
    {
        // ── Flavor pools (picked by seeded rng for replay variety) ────────────

        static readonly string[] s_auditBodies =
        {
            "A tax assessor from the capital has arrived asking pointed questions about provincial revenue.",
            "Word reaches you: imperial accountants have been examining the trade ledgers from last season.",
            "A courier bearing the imperial seal arrived this morning. The auditors will call within the week.",
        };

        static readonly string[] s_rivalBodies =
        {
            "A rival organization has sent word: pay tribute or they will undermine your network.",
            "One of your street contacts reports a rival gang moving into the eastern quarter. They want a cut.",
            "A well-dressed stranger left a sealed note at the palace gate. The terms are clear: tribute, or consequences.",
        };

        // Fired when a rival town holds a dominant traffic share — they have the coin to back the threat.
        static readonly string[] s_rivalBodiesHigh =
        {
            "A rival prefecture is flush with diverted trade revenue — their criminal arm now has the reach to threaten yours.",
            "With merchant caravans bypassing your town, a bolder rival organization has moved in to press an advantage.",
            "Your informants confirm it: a rival prefect, enriched by growing route traffic, is funding enforcers against your operation.",
        };

        static readonly string[] s_merchantBodies =
        {
            "The guild master has filed a formal protest: traders are avoiding the route due to extortion.",
            "Three prominent merchants have petitioned the provincial council, citing irregular taxation on the road.",
            "A delegation from the Merchant Brotherhood arrived at your offices. Business on the route is down sharply.",
        };

        static readonly string[] s_inspectorBodies =
        {
            "An inspector from the capital tours the province. His report could make or break your position.",
            "The Emperor has dispatched his personal auditor to review provincial administration. He arrives tomorrow.",
            "A fastidious official with capital connections has taken up residence at the inn. He has been asking questions.",
        };

        // ── IEventModel ───────────────────────────────────────────────────────

        public PendingEvent TryFireEvent(WorldState state, SimConfig config, Random rng)
        {
            if (!config.EnableEvents) return null;
            if (state.PendingEvent != null) return null;

            if (state.Tick >= config.InspectorVisitStartTick
                && rng.NextDouble() < config.InspectorVisitChance)
                return MakeInspectorVisit(config, rng);

            if (state.Heat > config.AuditWarningHeatThreshold
                && rng.NextDouble() < config.AuditWarningChance)
                return MakeAuditWarning(config, rng);

            if (state.OrganizedCrimeLevel >= 1)
            {
                float maxRivalShare = 0f;
                if (state.RivalTowns != null)
                    foreach (var r in state.RivalTowns)
                        if (r.TrafficShare > maxRivalShare) maxRivalShare = r.TrafficShare;
                float incursionChance = config.RivalIncursionChance
                    + maxRivalShare * config.RivalIncursionPressurePerSharePoint;
                if (rng.NextDouble() < incursionChance)
                    return MakeRivalIncursion(config, rng, maxRivalShare);
            }

            if (state.Reputation < config.MerchantComplaintRepThreshold
                && rng.NextDouble() < config.MerchantComplaintChance)
                return MakeMerchantComplaint(config, rng);

            return null;
        }

        public void ResolveEvent(WorldState state, EventOption choice, SimConfig config)
        {
            if (state.PendingEvent == null) return;

            if (choice != EventOption.None)
            {
                switch (state.PendingEvent.Type)
                {
                    case EventType.AuditWarning:
                        if (choice == EventOption.OptionA)
                        {
                            state.Purse = Math.Max(0f, state.Purse - config.AuditWarningBribeCost);
                            state.Heat  = Math.Max(0f, state.Heat  - config.AuditWarningBribeHeatReduction);
                        }
                        else
                        {
                            state.Heat += config.AuditWarningIgnoreHeatPenalty;
                        }
                        break;

                    case EventType.RivalIncursion:
                        if (choice == EventOption.OptionA)
                            state.Purse = Math.Max(0f, state.Purse - config.RivalIncursionTributeCost);
                        else
                            state.Safety = Math.Max(0f, state.Safety - config.RivalIncursionRefuseSafetyPenalty);
                        break;

                    case EventType.MerchantComplaint:
                        if (choice == EventOption.OptionA)
                        {
                            state.Purse      = Math.Max(0f, state.Purse      - config.MerchantComplaintCompensationCost);
                            state.Reputation = Math.Min(1f, state.Reputation + config.MerchantComplaintCompensationRepBonus);
                        }
                        else
                        {
                            state.Reputation = Math.Max(0f, state.Reputation - config.MerchantComplaintDismissRepPenalty);
                        }
                        break;

                    case EventType.InspectorVisit:
                        if (choice == EventOption.OptionA)
                        {
                            state.Purse = Math.Max(0f, state.Purse - config.InspectorGiftCost);
                            state.Heat  = Math.Max(0f, state.Heat  - config.InspectorGiftHeatReduction);
                        }
                        else
                        {
                            state.Heat += config.InspectorStonewallHeatPenalty;
                        }
                        break;
                }
            }

            state.PendingEvent = null;
        }

        // ── Event constructors ────────────────────────────────────────────────

        static PendingEvent MakeAuditWarning(SimConfig c, Random rng) => new PendingEvent
        {
            Type         = EventType.AuditWarning,
            Headline     = "Imperial Auditors Spotted",
            BodyText     = s_auditBodies[rng.Next(s_auditBodies.Length)],
            OptionALabel = $"Spread coin  (-§{c.AuditWarningBribeCost:0} purse, -{c.AuditWarningBribeHeatReduction:0} heat)",
            OptionBLabel = $"Stonewall them  (+{c.AuditWarningIgnoreHeatPenalty:0} heat)",
            OptionACost  = c.AuditWarningBribeCost,
            OptionBEffect= c.AuditWarningIgnoreHeatPenalty,
        };

        static PendingEvent MakeRivalIncursion(SimConfig c, Random rng, float maxRivalShare = 0f)
        {
            var pool = maxRivalShare > 0.24f ? s_rivalBodiesHigh : s_rivalBodies;
            return new PendingEvent
            {
                Type         = EventType.RivalIncursion,
                Headline     = "Rival Gang Demands Tribute",
                BodyText     = pool[rng.Next(pool.Length)],
                OptionALabel = $"Pay tribute  (-§{c.RivalIncursionTributeCost:0} purse)",
                OptionBLabel = $"Refuse  (-{c.RivalIncursionRefuseSafetyPenalty:P0} safety)",
                OptionACost  = c.RivalIncursionTributeCost,
                OptionBEffect= c.RivalIncursionRefuseSafetyPenalty,
            };
        }

        static PendingEvent MakeMerchantComplaint(SimConfig c, Random rng) => new PendingEvent
        {
            Type         = EventType.MerchantComplaint,
            Headline     = "Merchant Guild Complains",
            BodyText     = s_merchantBodies[rng.Next(s_merchantBodies.Length)],
            OptionALabel = $"Compensate guild  (-§{c.MerchantComplaintCompensationCost:0} purse)",
            OptionBLabel = $"Dismiss complaint  (-{c.MerchantComplaintDismissRepPenalty:P0} rep)",
            OptionACost  = c.MerchantComplaintCompensationCost,
            OptionBEffect= c.MerchantComplaintDismissRepPenalty,
        };

        static PendingEvent MakeInspectorVisit(SimConfig c, Random rng) => new PendingEvent
        {
            Type         = EventType.InspectorVisit,
            Headline     = "Visiting Imperial Inspector",
            BodyText     = s_inspectorBodies[rng.Next(s_inspectorBodies.Length)],
            OptionALabel = $"Gift the inspector  (-§{c.InspectorGiftCost:0} purse, -{c.InspectorGiftHeatReduction:0} heat)",
            OptionBLabel = $"Stonewall  (+{c.InspectorStonewallHeatPenalty:0} heat)",
            OptionACost  = c.InspectorGiftCost,
            OptionBEffect= c.InspectorStonewallHeatPenalty,
        };
    }
}
