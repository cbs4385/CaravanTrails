using GameCore.Events;

namespace GameCore.Sim
{
    public class TelemetryRecord
    {
        public int Tick;
        public float Purse;
        public float Coffers;
        public float TownQuality;
        public float Safety;
        public float Reputation;
        public float Heat;
        public float TaxRate;
        public float SkimFraction;
        public float TrafficVolume;
        public float OfficialRevenue;
        public float SkimmedAmount;
        public float CoffersContribution;
        public float CrimeYield;
        public int OrganizedCrimeLevel;
        public float RivalPressure;
        public float RouteAttractiveness;
        public bool AuditFired;
        public bool BetrayalFired;
        public EndReason EndReason;
        public EventType EventFired;

        public static string CsvHeader =>
            "tick,purse,coffers,town_quality,safety,reputation,heat," +
            "tax_rate,skim_fraction,traffic_volume,official_revenue," +
            "skimmed_amount,coffers_contribution,crime_yield," +
            "organized_crime_level,rival_pressure,route_attractiveness," +
            "audit_fired,betrayal_fired,end_reason,event_fired";

        public string ToCsvRow() =>
            $"{Tick},{Purse:F2},{Coffers:F2},{TownQuality:F3},{Safety:F3}," +
            $"{Reputation:F3},{Heat:F3},{TaxRate:F3},{SkimFraction:F3}," +
            $"{TrafficVolume:F2},{OfficialRevenue:F2},{SkimmedAmount:F2}," +
            $"{CoffersContribution:F2},{CrimeYield:F2},{OrganizedCrimeLevel}," +
            $"{RivalPressure:F3},{RouteAttractiveness:F3}," +
            $"{(AuditFired ? 1 : 0)},{(BetrayalFired ? 1 : 0)},{EndReason},{EventFired}";
    }
}
