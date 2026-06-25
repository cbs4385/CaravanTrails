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

        // End state
        public bool IsGameOver;
        public EndReason EndReason;

        // Active event awaiting player response (null = none pending)
        public PendingEvent PendingEvent;

        public WorldState Clone() => (WorldState)MemberwiseClone();

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
