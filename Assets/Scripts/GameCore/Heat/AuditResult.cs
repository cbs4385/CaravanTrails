namespace GameCore.Heat
{
    public class AuditResult
    {
        public bool AuditOccurred;
        public bool AuditFailed;
        public float FineAmount;

        public static AuditResult NoAudit => new AuditResult { AuditOccurred = false };
    }
}
