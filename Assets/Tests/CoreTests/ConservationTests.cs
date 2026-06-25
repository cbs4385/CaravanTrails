using NUnit.Framework;
using GameCore.Sim;

namespace CoreTests
{
    // §8.8: coins are neither created nor destroyed except where intended.
    public class ConservationTests
    {
        private static SimConfig ZeroNoiseConfig() => new SimConfig
        {
            TrafficNoiseMagnitude = 0f,
            TributePerTick = 0f,
            HeatNaturalDecayPerTick = 0f,
            HeatAccrualPerSkimUnit = 0f,
            UnorganizedCrimeHeatPerYieldUnit = 0f,
            OrganizedCrimeHeatPerLevel = 0f,
            MissedTributeHeatSpike = 0f,
            LegitimacyHeatBufferPerCoffersUnit = 0f,
        };

        [Test]
        public void SkimPlusCoffersMustEqualOfficialRevenue()
        {
            var sim = new Simulator(ZeroNoiseConfig(), seed: 0);
            var input = new PlayerInput
            {
                TaxRate = 0.20f,
                SkimFraction = 0.30f,
                UnorganizedCrimeIntensity = 0f,
                BribeAmount = 0f,
            };

            var result = sim.Tick(input);

            Assert.AreEqual(
                result.Telemetry.OfficialRevenue,
                result.Telemetry.SkimmedAmount + result.Telemetry.CoffersContribution,
                0.001f,
                "Skim + coffers contribution must equal total official revenue.");
        }

        [Test]
        public void PurseGrowsByExactlySkimPlusCrime_WhenNoOtherDrains()
        {
            var config = ZeroNoiseConfig();
            config.OrganizedCrimeUpkeepPerLevel = 0f;
            config.EnableAuditFail = false;  // no surprise fine

            var sim = new Simulator(config, seed: 1);
            float purseBefore = sim.State.Purse;

            var input = new PlayerInput
            {
                TaxRate = 0.10f,
                SkimFraction = 0.20f,
                UnorganizedCrimeIntensity = 0.5f,
                BribeAmount = 0f,
                Upgrade = UpgradePurchase.None,
            };

            var result = sim.Tick(input);

            float expectedPurse = purseBefore
                + result.Telemetry.SkimmedAmount
                + result.Telemetry.CrimeYield;

            Assert.AreEqual(expectedPurse, result.StateAfter.Purse, 0.01f,
                "Purse must grow by exactly skim + crime yield with no other drains.");
        }

        [Test]
        public void ZeroTaxRate_ProducesZeroOfficialRevenue()
        {
            var sim = new Simulator(ZeroNoiseConfig(), seed: 2);
            var input = new PlayerInput { TaxRate = 0f, SkimFraction = 0.5f };

            var result = sim.Tick(input);

            Assert.AreEqual(0f, result.Telemetry.OfficialRevenue, 0.001f,
                "Zero tax rate should produce zero official revenue.");
            Assert.AreEqual(0f, result.Telemetry.SkimmedAmount, 0.001f,
                "Zero revenue means zero skim.");
        }

        [Test]
        public void FullSkimFraction_LeavesNothing_InCoffers()
        {
            var sim = new Simulator(ZeroNoiseConfig(), seed: 3);
            var input = new PlayerInput { TaxRate = 0.25f, SkimFraction = 1.0f };

            var result = sim.Tick(input);

            Assert.AreEqual(0f, result.Telemetry.CoffersContribution, 0.001f,
                "100% skim fraction should leave nothing for the coffers.");
        }
    }
}
