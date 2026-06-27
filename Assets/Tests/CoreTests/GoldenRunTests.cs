using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using GameCore.Sim;

namespace CoreTests
{
    // §8.8: fixed seed + config + inputs => known telemetry hash.
    // Acts as a change-detector: any edit that alters simulation output changes the hash.
    // When you intentionally retune balance, update ExpectedHash to the new value
    // (run the test once to see the actual hash in the failure message).
    public class GoldenRunTests
    {
        // Recompute with: seed=42, default SimConfig, 50 ticks, TaxRate=0.15 SkimFraction=0.10
        // Updated after adding seasonal events (SeasonalHarvest fires at tick≥25, mask=1 at end)
        private const string ExpectedHash =
            "693110caf98bb39f4fe4dd909c35350068268f2a167f7ee332b1e36328f12866";

        [Test]
        public void GoldenRun_TelemetryHashMatchesBaseline()
        {
            var sim   = new Simulator(new SimConfig(), seed: 42);
            var input = new PlayerInput { TaxRate = 0.15f, SkimFraction = 0.10f };

            for (int i = 0; i < 50 && !sim.State.IsGameOver; i++)
                sim.Tick(input);

            string actualHash = HashTelemetry(sim.Telemetry);

            Assert.AreEqual(ExpectedHash, actualHash,
                $"Telemetry hash changed (actual: {actualHash}). " +
                "If this is an intentional balance retune, update ExpectedHash in GoldenRunTests.cs.");
        }

        private static string HashTelemetry(IReadOnlyList<TelemetryRecord> telemetry)
        {
            var sb = new StringBuilder();
            foreach (var row in telemetry)
                sb.AppendLine(row.ToCsvRow());

            byte[] bytes     = Encoding.UTF8.GetBytes(sb.ToString());
            byte[] hashBytes = SHA256.Create().ComputeHash(bytes);
            return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
        }
    }
}
