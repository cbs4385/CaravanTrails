using NUnit.Framework;
using GameCore.Sim;
using GameCore.Economy;

namespace CoreTests
{
    // §6.1: verify that tax elasticity produces an interior revenue optimum, not a monotone line.
    public class TaxElasticityTests
    {
        [Test]
        public void Revenue_PeaksAtInteriorTaxRate_NotAtMaximum()
        {
            var config = new SimConfig
            {
                TaxElasticityK = 2f,
                TrafficNoiseMagnitude = 0f,
                BaseTrafficVolume = 100f,
                AttractivenessWeightTax = 1f,
                AttractivenessWeightSafety = 0f,
                AttractivenessWeightTownQuality = 0f,
                AttractivenessWeightReputation = 0f,
            };

            var state = WorldState.Default();

            float peakRevenue = float.MinValue;
            float peakTaxRate = 0f;

            for (int i = 1; i <= 20; i++)
            {
                float taxRate = i * 0.05f;
                float attractiveness = TrafficModel.ComputeAttractiveness(state, taxRate, config);
                float traffic = config.BaseTrafficVolume * attractiveness;
                float revenue = TaxationModel.ComputeOfficialRevenue(traffic, taxRate);

                if (revenue > peakRevenue)
                {
                    peakRevenue = revenue;
                    peakTaxRate = taxRate;
                }
            }

            Assert.Greater(peakTaxRate, 0.05f, "Revenue should not peak at the lowest tax rate.");
            Assert.Less(peakTaxRate, 1.0f, "Revenue should not peak at 100% tax.");
        }

        [Test]
        public void HigherK_ShiftsOptimum_ToLowerTaxRate()
        {
            var stateA = WorldState.Default();
            var stateB = WorldState.Default();

            float FindPeakTax(float k)
            {
                var cfg = new SimConfig
                {
                    TaxElasticityK = k,
                    TrafficNoiseMagnitude = 0f,
                    BaseTrafficVolume = 100f,
                    AttractivenessWeightTax = 1f,
                    AttractivenessWeightSafety = 0f,
                    AttractivenessWeightTownQuality = 0f,
                    AttractivenessWeightReputation = 0f,
                };
                var state = WorldState.Default();
                float peak = 0f, peakTax = 0f;
                for (int i = 1; i <= 50; i++)
                {
                    float t = i * 0.02f;
                    float a = TrafficModel.ComputeAttractiveness(state, t, cfg);
                    float r = cfg.BaseTrafficVolume * a * t;
                    if (r > peak) { peak = r; peakTax = t; }
                }
                return peakTax;
            }

            float peakLowK = FindPeakTax(1f);
            float peakHighK = FindPeakTax(4f);

            Assert.Greater(peakLowK, peakHighK,
                "Higher elasticity k should shift the revenue-maximizing tax rate downward.");
        }
    }
}
