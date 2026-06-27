using GameCore.Events;

namespace GameCore.Sim
{
    // Per-tick intermediate values — not persistent state, but passed between systems within a tick.
    public class TickContext
    {
        public float RouteAttractiveness;
        public float TrafficVolume;
        public float OfficialRevenue;
        public float SkimmedAmount;
        public float CoffersContribution;
        public float CrimeYield;
        public float UnorganizedCrimeYield;
        public float RivalPressure;
        public bool BetrayalOccurred;
        public bool AuditOccurred;
        public bool AuditFailed;
        public float AuditFineAmount;
        public float PlayerTrafficShare;  // fraction of network traffic this tick (1.0 when rivals disabled)
        public EventType EventFired = EventType.None;
    }
}
