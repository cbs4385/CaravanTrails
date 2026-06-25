using System;
using GameCore.Events;

namespace GameCore.Sim
{
    [Serializable]
    public class PlayerInput
    {
        public float TaxRate;                   // 0–1
        public float SkimFraction;              // 0–1 (fraction of official revenue diverted to purse)
        public float UnorganizedCrimeIntensity; // 0–1 (scales unorganized yield)
        public int OrganizedCrimeLevelDelta;    // how many levels to invest in this tick (cost deducted from purse)
        public UpgradePurchase Upgrade;
        public float BribeAmount;               // purse spend → heat reduction
        public EventOption EventChoice;         // response to PendingEvent from last tick

        public static PlayerInput Passive => new PlayerInput
        {
            TaxRate = 0.15f,
            SkimFraction = 0f,
            UnorganizedCrimeIntensity = 0f,
            OrganizedCrimeLevelDelta = 0,
            Upgrade = UpgradePurchase.None,
            BribeAmount = 0f,
        };

        public static PlayerInput Greedy => new PlayerInput
        {
            TaxRate = 0.40f,
            SkimFraction = 0.5f,
            UnorganizedCrimeIntensity = 1f,
            OrganizedCrimeLevelDelta = 0,
            Upgrade = UpgradePurchase.None,
            BribeAmount = 0f,
        };
    }

    public enum UpgradePurchase
    {
        None,
        Collection,
        HeatDecay,
    }
}
