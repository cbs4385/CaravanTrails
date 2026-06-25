namespace GameCore.Sim
{
    public class TickResult
    {
        public WorldState StateAfter;
        public TelemetryRecord Telemetry;
        public bool AuditOccurred;
        public bool BetrayalOccurred;
    }
}
