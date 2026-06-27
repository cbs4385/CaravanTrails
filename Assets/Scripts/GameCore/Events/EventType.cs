namespace GameCore.Events
{
    public enum EventType
    {
        None,
        AuditWarning,
        RivalIncursion,
        MerchantComplaint,
        InspectorVisit,
        TradeDelegation,
        DivertedCaravan,
        // One-shot seasonal beats — fire once per run at tick thresholds
        SeasonalHarvest,
        SeasonalGovernorVisit,
        SeasonalBanditSurge,
        SeasonalAuditSeason,
    }
}
