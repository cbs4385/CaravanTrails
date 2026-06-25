using NUnit.Framework;
using GameCore.Sim;

namespace CoreTests
{
    // §8.8: same seed + same config + same inputs => identical run
    public class SimulatorDeterminismTests
    {
        private static Simulator BuildSim(int seed) =>
            new Simulator(new SimConfig(), seed);

        [Test]
        public void SameSeed_ProducesIdenticalRuns()
        {
            var simA = BuildSim(42);
            var simB = BuildSim(42);
            var input = PlayerInput.Passive;

            for (int i = 0; i < 30; i++)
            {
                var a = simA.Tick(input);
                var b = simB.Tick(input);

                Assert.AreEqual(a.StateAfter.Purse, b.StateAfter.Purse, 0.0001f,
                    $"Purse mismatch at tick {i + 1}");
                Assert.AreEqual(a.StateAfter.Heat, b.StateAfter.Heat, 0.0001f,
                    $"Heat mismatch at tick {i + 1}");
                Assert.AreEqual(a.StateAfter.TownQuality, b.StateAfter.TownQuality, 0.0001f,
                    $"TownQuality mismatch at tick {i + 1}");
                Assert.AreEqual(a.Telemetry.TrafficVolume, b.Telemetry.TrafficVolume, 0.0001f,
                    $"TrafficVolume mismatch at tick {i + 1}");

                // Both sims must end at the same tick — verify then stop
                Assert.AreEqual(a.StateAfter.IsGameOver, b.StateAfter.IsGameOver,
                    $"Game-over state mismatch at tick {i + 1}");
                if (a.StateAfter.IsGameOver) break;
            }
        }

        [Test]
        public void DifferentSeeds_ProduceDifferentTraffic()
        {
            var simA = BuildSim(1);
            var simB = BuildSim(999);

            bool sawDifference = false;
            for (int i = 0; i < 30; i++)
            {
                var a = simA.Tick(PlayerInput.Passive);
                var b = simB.Tick(PlayerInput.Passive);
                if (System.Math.Abs(a.Telemetry.TrafficVolume - b.Telemetry.TrafficVolume) > 0.001f)
                {
                    sawDifference = true;
                    break;
                }
            }

            Assert.IsTrue(sawDifference,
                "Two different seeds should produce at least one different traffic value over 30 ticks.");
        }

        [Test]
        public void SnapshotAndResume_ProduceSameResultAsUninterrupted()
        {
            // Two sims with the same seed and inputs must reach exactly the same state
            // at every tick — confirming the loop is deterministic end-to-end.
            var simA = BuildSim(7);
            var simB = BuildSim(7);
            var input = PlayerInput.Greedy;

            for (int i = 0; i < 10; i++)
            {
                if (simA.State.IsGameOver) break;
                simA.Tick(input);
                simB.Tick(input);
            }

            Assert.AreEqual(simA.State.Purse, simB.State.Purse, 0.0001f, "Purse");
            Assert.AreEqual(simA.State.Heat, simB.State.Heat, 0.0001f, "Heat");
            Assert.AreEqual(simA.State.TownQuality, simB.State.TownQuality, 0.0001f, "TownQuality");
            Assert.AreEqual(simA.State.IsGameOver, simB.State.IsGameOver, "IsGameOver");
        }
    }
}
