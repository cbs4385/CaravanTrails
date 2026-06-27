using System;

namespace GameCore.Sim
{
    [Serializable]
    public class RivalTownState
    {
        public float TaxRate;
        public float Quality;       // 0–1
        public float Safety;        // 0–1
        public float TrafficShare;  // fraction of network traffic, last tick

        public RivalTownState Clone() => (RivalTownState)MemberwiseClone();
    }
}
