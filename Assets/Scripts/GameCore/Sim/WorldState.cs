using System;
using GameCore.Events;

namespace GameCore.Sim
{
    [Serializable]
    public class WorldState
    {
        // Time
        public int Tick;

        // Player resources
        public float Purse;
        public float Coffers;

        // Town condition (0–1)
        public float TownQuality;
        public float Safety;
        public float Reputation;   // lagged merchant perception

        // Suspicion stock
        public float Heat;

        // Investment levels
        public int OrganizedCrimeLevel;
        public int CollectionUpgradeLevel;
        public int HeatDecayUpgradeLevel;
        public int TownInvestmentLevel;
        public int RouteImprovementLevel;
        public int ConnectionsLevel;

        // End state
        public bool IsGameOver;
        public EndReason EndReason;

        // Active event awaiting player response (null = none pending)
        public PendingEvent PendingEvent;

        // Rival towns competing for road traffic (null when EnableRivals = false)
        public RivalTownState[] RivalTowns;

        // Ticks remaining on an active trade deal (0 = no deal active)
        public int TradeDealTicksRemaining;

        public WorldState Clone()
        {
            var c = (WorldState)MemberwiseClone();
            if (RivalTowns != null)
            {
                c.RivalTowns = new RivalTownState[RivalTowns.Length];
                for (int i = 0; i < RivalTowns.Length; i++)
                    c.RivalTowns[i] = RivalTowns[i]?.Clone();
            }
            return c;
        }

        public static WorldState Default() => new WorldState
        {
            Tick = 0,
            Purse = 0f,
            Coffers = 50f,
            TownQuality = 0.6f,
            Safety = 0.7f,
            Reputation = 0.7f,
            Heat = 0f,
            OrganizedCrimeLevel = 0,
            CollectionUpgradeLevel = 0,
            HeatDecayUpgradeLevel = 0,
            TownInvestmentLevel = 0,
            RouteImprovementLevel = 0,
            ConnectionsLevel = 0,
            IsGameOver = false,
            EndReason = EndReason.None,
        };
    }

    public enum EndReason
    {
        None,
        AuditArrest,
        BankruptcyCollapse,
        RivalOverthrow,
        WealthWin,
    }
}
