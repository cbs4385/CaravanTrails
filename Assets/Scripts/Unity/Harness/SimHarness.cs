using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using GameCore.Sim;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

// §8.5 experiment harness.
// Batchmode:  unity -batchmode -projectPath <path> -executeMethod SimHarness.RunSweep -logFile -
// In-editor:  Tools → Run Sweep Harness
// Output:     <project>/Telemetry/{sweep}.csv          per-tick detail
//             <project>/Telemetry/{sweep}_runs.csv     one row per (config × seed)
//             <project>/Telemetry/{sweep}_metrics.csv  §7 derived metrics per config
public static class SimHarness
{
    // ── Entry points ─────────────────────────────────────────────────────────

    public static void RunSweep()  // batchmode
    {
        RunAllSweeps();
        Application.Quit(0);
    }

    public static void RunFromEditor() => RunAllSweeps();

#if UNITY_EDITOR
    [MenuItem("Tools/Run Sweep Harness")]
    private static void RunFromEditorMenuItem() => RunAllSweeps();
#endif

    // ── Sweep definitions ────────────────────────────────────────────────────

    private static void RunAllSweeps()
    {
        string outputDir = Path.Combine(Application.dataPath, "..", "Telemetry");
        Directory.CreateDirectory(outputDir);

        foreach (var sweep in BuildSweeps())
        {
            Debug.Log($"[SimHarness] '{sweep.Name}' — {sweep.Configs.Count} configs × {sweep.SeedCount} seeds × {sweep.MaxTicks} ticks");
            var summaries = Execute(sweep);
            WriteDetailCsv(outputDir, sweep.Name, summaries);
            WriteRunsCsv(outputDir, sweep.Name, summaries);
            WriteMetricsCsv(outputDir, sweep.Name, summaries);
            Debug.Log($"[SimHarness] Done. {summaries.Count} runs → Telemetry/{sweep.Name}*.csv");
        }
    }

    private static List<SweepSpec> BuildSweeps()
    {
        var sweeps = new List<SweepSpec>();

        // §6.1 — Tax elasticity: does a Laffer optimum exist?
        var taxSweep = new SweepSpec { Name = "tax_elasticity", SeedCount = 10, MaxTicks = 100 };
        foreach (float taxRate in new[] { 0.05f, 0.10f, 0.15f, 0.20f, 0.25f, 0.30f, 0.40f, 0.50f })
        {
            float t = taxRate;
            taxSweep.Configs.Add(new ConfigEntry
            {
                Id = $"tax_{t:F2}",
                Config = new SimConfig { TrafficNoiseMagnitude = 0.05f },
                CreateStrategy = () => state => new PlayerInput { TaxRate = t, SkimFraction = 0.10f },
            });
        }
        sweeps.Add(taxSweep);

        // §6.2 — Coffers tension: skim-everything vs balanced investment
        var skimSweep = new SweepSpec { Name = "skim_fraction", SeedCount = 10, MaxTicks = 200 };
        foreach (float skim in new[] { 0f, 0.10f, 0.20f, 0.30f, 0.50f, 0.75f, 1.0f })
        {
            float s = skim;
            skimSweep.Configs.Add(new ConfigEntry
            {
                Id = $"skim_{s:F2}",
                Config = new SimConfig(),
                CreateStrategy = () => state => new PlayerInput { TaxRate = 0.20f, SkimFraction = s },
            });
        }
        sweeps.Add(skimSweep);

        // §6.3 — Organized crime interior optimum
        // Strategies: save-up phase (skim=0.40, no bribe) then crime phase (skim=0.10, bribe active).
        var crimeSweep = new SweepSpec { Name = "organized_crime", SeedCount = 20, MaxTicks = 200 };

        crimeSweep.Configs.Add(new ConfigEntry
        {
            Id = "pure_skim20",
            Config = new SimConfig(),
            CreateStrategy = () => state => new PlayerInput { TaxRate = 0.20f, SkimFraction = 0.20f },
        });

        foreach (var spec in new[]
        {
            new { id = "org1_smart", target = 1, bribe = 3f },
            new { id = "org2_smart", target = 2, bribe = 5f },
            new { id = "org3_smart", target = 3, bribe = 7f },
        })
        {
            int orgTarget    = spec.target;
            float bribeAmt   = spec.bribe;
            float setupCost  = new SimConfig().OrganizedCrimeSetupCostPerLevel;
            float setupNeeded = orgTarget * setupCost;

            crimeSweep.Configs.Add(new ConfigEntry
            {
                Id = spec.id,
                Config = new SimConfig(),
                // Factory produces a fresh stateful strategy per seed — avoids closure capture bugs.
                CreateStrategy = () =>
                {
                    bool inCrimePhase = false;
                    return state =>
                    {
                        if (!inCrimePhase && state.Purse >= setupNeeded)
                            inCrimePhase = true;

                        int delta = 0;
                        if (inCrimePhase && state.OrganizedCrimeLevel < orgTarget && state.Purse >= setupCost)
                            delta = 1;

                        return new PlayerInput
                        {
                            TaxRate  = 0.20f,
                            SkimFraction = inCrimePhase ? 0.10f : 0.40f,
                            OrganizedCrimeLevelDelta = delta,
                            BribeAmount = inCrimePhase ? bribeAmt : 0f,
                        };
                    };
                },
            });
        }
        sweeps.Add(crimeSweep);

        // §3 Event impact — org2-level strategy with three event response policies.
        // Isolates the net cost of events from strategy differences.
        var eventSweep = new SweepSpec { Name = "event_impact", SeedCount = 20, MaxTicks = 200 };

        foreach (var spec in new[]
        {
            new { id = "org2_dismiss",     eventOpt = 0 },  // auto-dismiss (baseline)
            new { id = "org2_pay_all",     eventOpt = 1 },  // always OptionA (pay/comply)
            new { id = "org2_refuse_all",  eventOpt = 2 },  // always OptionB (stonewall/refuse)
        })
        {
            int  opt        = spec.eventOpt;
            int  orgTarget  = 2;
            float bribeAmt  = 5f;
            float setupCost = new SimConfig().OrganizedCrimeSetupCostPerLevel;
            float setupNeeded = orgTarget * setupCost;

            eventSweep.Configs.Add(new ConfigEntry
            {
                Id = spec.id,
                Config = new SimConfig(),
                CreateStrategy = () =>
                {
                    bool inCrimePhase = false;
                    return state =>
                    {
                        if (!inCrimePhase && state.Purse >= setupNeeded)
                            inCrimePhase = true;

                        int delta = 0;
                        if (inCrimePhase && state.OrganizedCrimeLevel < orgTarget && state.Purse >= setupCost)
                            delta = 1;

                        GameCore.Events.EventOption choice = GameCore.Events.EventOption.None;
                        if (state.PendingEvent != null)
                        {
                            if      (opt == 1) choice = GameCore.Events.EventOption.OptionA;
                            else if (opt == 2) choice = GameCore.Events.EventOption.OptionB;
                        }

                        return new PlayerInput
                        {
                            TaxRate                  = 0.20f,
                            SkimFraction             = inCrimePhase ? 0.10f : 0.40f,
                            OrganizedCrimeLevelDelta = delta,
                            BribeAmount              = inCrimePhase ? bribeAmt : 0f,
                            EventChoice              = choice,
                        };
                    };
                },
            });
        }
        sweeps.Add(eventSweep);

        return sweeps;
    }

    // ── Execution engine ─────────────────────────────────────────────────────

    private static List<RunSummary> Execute(SweepSpec sweep)
    {
        var summaries = new List<RunSummary>();

        foreach (var cfg in sweep.Configs)
        {
            for (int seed = 0; seed < sweep.SeedCount; seed++)
            {
                var sim      = new Simulator(cfg.Config, seed);
                var strategy = cfg.CreateStrategy();  // fresh instance per seed

                while (!sim.State.IsGameOver && sim.State.Tick < sweep.MaxTicks)
                    sim.Tick(strategy(sim.State));

                summaries.Add(BuildSummary(sweep.Name, cfg.Id, seed, cfg.Config, sim));
            }
        }

        return summaries;
    }

    private static RunSummary BuildSummary(
        string sweepName, string configId, int seed, SimConfig config, Simulator sim)
    {
        float peakPurse       = 0f;
        float peakHeat        = 0f;
        int   firstHeat75Tick = -1;
        float totalSkimmed    = 0f;
        float totalCoffers    = 0f;
        int   totalEvents     = 0;
        float threshold75     = config.AuditThreshold * 0.75f;

        foreach (var row in sim.Telemetry)
        {
            if (row.Purse > peakPurse)  peakPurse = row.Purse;
            if (row.Heat  > peakHeat)   peakHeat  = row.Heat;
            if (firstHeat75Tick < 0 && row.Heat >= threshold75)
                firstHeat75Tick = row.Tick;
            totalSkimmed += row.SkimmedAmount;
            totalCoffers += row.CoffersContribution;
            if (row.EventFired != GameCore.Events.EventType.None)
                totalEvents++;
        }

        return new RunSummary
        {
            SweepName              = sweepName,
            ConfigId               = configId,
            Seed                   = seed,
            FinalTick              = sim.State.Tick,
            FinalPurse             = sim.State.Purse,
            PeakPurse              = peakPurse,
            FinalHeat              = sim.State.Heat,
            PeakHeat               = peakHeat,
            FirstHeat75Tick        = firstHeat75Tick,
            FinalTownQuality       = sim.State.TownQuality,
            FinalSafety            = sim.State.Safety,
            EndReason              = sim.State.EndReason,
            TotalSkimmed           = totalSkimmed,
            TotalCoffersContribution = totalCoffers,
            TotalEventsFired       = totalEvents,
            Telemetry              = new List<TelemetryRecord>(sim.Telemetry),
        };
    }

    // ── CSV writers ──────────────────────────────────────────────────────────

    // Per-tick detail: one row per (sweep × config × seed × tick).
    private static void WriteDetailCsv(string dir, string sweepName, List<RunSummary> summaries)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"sweep,config_id,seed,{TelemetryRecord.CsvHeader}");
        foreach (var run in summaries)
            foreach (var row in run.Telemetry)
                sb.AppendLine($"{run.SweepName},{run.ConfigId},{run.Seed},{row.ToCsvRow()}");
        File.WriteAllText(Path.Combine(dir, $"{sweepName}.csv"), sb.ToString());
    }

    // Per-run summary: one row per (config × seed) with §7 proxies.
    private static void WriteRunsCsv(string dir, string sweepName, List<RunSummary> summaries)
    {
        var sb = new StringBuilder();
        sb.AppendLine(
            "sweep,config_id,seed," +
            "final_tick,final_purse,peak_purse," +
            "final_heat,peak_heat,first_heat75_tick," +
            "final_town_quality,final_safety,end_reason," +
            "total_skimmed,total_coffers_contribution,wealth_win");

        foreach (var r in summaries)
            sb.AppendLine(
                $"{r.SweepName},{r.ConfigId},{r.Seed}," +
                $"{r.FinalTick},{r.FinalPurse:F2},{r.PeakPurse:F2}," +
                $"{r.FinalHeat:F2},{r.PeakHeat:F2},{r.FirstHeat75Tick}," +
                $"{r.FinalTownQuality:F3},{r.FinalSafety:F3},{r.EndReason}," +
                $"{r.TotalSkimmed:F2},{r.TotalCoffersContribution:F2}," +
                $"{(r.EndReason == EndReason.WealthWin ? 1 : 0)}");

        File.WriteAllText(Path.Combine(dir, $"{sweepName}_runs.csv"), sb.ToString());
    }

    // §7 derived metrics: one row per config, aggregated across seeds.
    // Covers: strategy spread, risk arc proxy, pocket-vs-coffers ratio.
    private static void WriteMetricsCsv(string dir, string sweepName, List<RunSummary> summaries)
    {
        // Group by config_id, preserving insertion order
        var order  = new List<string>();
        var groups = new Dictionary<string, List<RunSummary>>();
        foreach (var r in summaries)
        {
            if (!groups.ContainsKey(r.ConfigId))
            {
                order.Add(r.ConfigId);
                groups[r.ConfigId] = new List<RunSummary>();
            }
            groups[r.ConfigId].Add(r);
        }

        var sb = new StringBuilder();
        sb.AppendLine(
            "sweep,config_id,n_runs," +
            "mean_final_tick,mean_final_purse,mean_peak_purse," +
            "wealth_win_pct,audit_arrest_pct,rival_overthrow_pct," +
            "mean_peak_heat,mean_first_heat75_tick," +
            "mean_final_safety,mean_total_skimmed,mean_total_coffers," +
            "mean_skim_to_coffers_ratio,mean_events_fired");

        foreach (var cfgId in order)
        {
            var runs = groups[cfgId];
            int n = runs.Count;

            float meanFinalTick    = Avg(runs, r => r.FinalTick);
            float meanFinalPurse   = Avg(runs, r => r.FinalPurse);
            float meanPeakPurse    = Avg(runs, r => r.PeakPurse);
            float wealthWinPct     = 100f * Count(runs, r => r.EndReason == EndReason.WealthWin)   / n;
            float auditPct         = 100f * Count(runs, r => r.EndReason == EndReason.AuditArrest) / n;
            float rivalPct         = 100f * Count(runs, r => r.EndReason == EndReason.RivalOverthrow) / n;
            float meanPeakHeat     = Avg(runs, r => r.PeakHeat);
            // first_heat75_tick: runs that never crossed the threshold get final_tick as proxy
            float meanFirstHeat75  = Avg(runs, r => r.FirstHeat75Tick >= 0 ? r.FirstHeat75Tick : r.FinalTick);
            float meanFinalSafety  = Avg(runs, r => r.FinalSafety);
            float meanTotalSkim    = Avg(runs, r => r.TotalSkimmed);
            float meanTotalCoffers  = Avg(runs, r => r.TotalCoffersContribution);
            float meanEventsFired   = Avg(runs, r => r.TotalEventsFired);
            // pocket-vs-coffers ratio: >1 means more skimmed than invested in town
            float skimToCoffersRatio = meanTotalCoffers > 0f ? meanTotalSkim / meanTotalCoffers : 0f;

            sb.AppendLine(
                $"{sweepName},{cfgId},{n}," +
                $"{meanFinalTick:F1},{meanFinalPurse:F1},{meanPeakPurse:F1}," +
                $"{wealthWinPct:F1},{auditPct:F1},{rivalPct:F1}," +
                $"{meanPeakHeat:F1},{meanFirstHeat75:F1}," +
                $"{meanFinalSafety:F3},{meanTotalSkim:F1},{meanTotalCoffers:F1}," +
                $"{skimToCoffersRatio:F3},{meanEventsFired:F1}");
        }

        File.WriteAllText(Path.Combine(dir, $"{sweepName}_metrics.csv"), sb.ToString());
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static float Avg(List<RunSummary> list, Func<RunSummary, float> f)
    {
        float sum = 0f;
        foreach (var r in list) sum += f(r);
        return sum / list.Count;
    }

    private static int Count(List<RunSummary> list, Func<RunSummary, bool> p)
    {
        int n = 0;
        foreach (var r in list) if (p(r)) n++;
        return n;
    }

    // ── Data types ────────────────────────────────────────────────────────────

    private class RunSummary
    {
        public string SweepName;
        public string ConfigId;
        public int    Seed;
        public int    FinalTick;
        public float  FinalPurse;
        public float  PeakPurse;
        public float  FinalHeat;
        public float  PeakHeat;
        public int    FirstHeat75Tick;        // tick heat first crossed 75% of audit threshold; -1 if never
        public float  FinalTownQuality;
        public float  FinalSafety;
        public EndReason EndReason;
        public float  TotalSkimmed;
        public float  TotalCoffersContribution;
        public int    TotalEventsFired;
        public List<TelemetryRecord> Telemetry;
    }

    private class SweepSpec
    {
        public string Name;
        public int    SeedCount = 10;
        public int    MaxTicks  = 200;
        public List<ConfigEntry> Configs = new List<ConfigEntry>();
    }

    private class ConfigEntry
    {
        public string   Id;
        public SimConfig Config;
        // Returns a fresh strategy closure each call — required for stateful strategies.
        public Func<Func<WorldState, PlayerInput>> CreateStrategy;
    }
}
