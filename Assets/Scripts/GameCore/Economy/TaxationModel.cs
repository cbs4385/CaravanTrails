using GameCore.Sim;

namespace GameCore.Economy
{
    // §4.2: official revenue = traffic * tax_rate; player splits it purse-vs-coffers via skim fraction
    public static class TaxationModel
    {
        public static float ComputeOfficialRevenue(float trafficVolume, float taxRate)
        {
            return trafficVolume * taxRate;
        }

        public static (float toPurse, float toCoffers) ApplySkim(
            float officialRevenue, float skimFraction, int collectionUpgradeLevel, SimConfig config)
        {
            float effectiveSkim = skimFraction * (1f + collectionUpgradeLevel * config.UpgradeCollectionSkimBonusPerLevel);
            if (effectiveSkim > 1f) effectiveSkim = 1f;

            float purseShare = officialRevenue * effectiveSkim;
            float coffersShare = officialRevenue - purseShare;
            return (purseShare, coffersShare);
        }
    }
}
