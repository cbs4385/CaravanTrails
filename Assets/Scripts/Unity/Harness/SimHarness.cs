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

        // §6.5 — Upgrade ROI: are the five upgrades worth buying on the org2+pay_all path?
        // Baseline = org2+pay_all (known 70% win). Each variant adds one upgrade target.
        var upgSweep = new SweepSpec { Name = "upgrade_roi", SeedCount = 20, MaxTicks = 200 };

        float upgSetupCost   = new SimConfig().OrganizedCrimeSetupCostPerLevel;
        float upgSetupNeeded = 2 * upgSetupCost;
        float coffersBuf     = new SimConfig().TributePerTick * 8f;  // 8-tick tribute safety margin

        foreach (var spec in new[]
        {
            new { id = "baseline",       purseTargets = new (UpgradePurchase, int)[0],                               coffersTargets = new (UpgradePurchase, int)[0]                               },
            new { id = "collection_l2",  purseTargets = new[] { (UpgradePurchase.Collection, 2) },                   coffersTargets = new (UpgradePurchase, int)[0]                               },
            new { id = "heat_decay_l1",  purseTargets = new[] { (UpgradePurchase.HeatDecay, 1) },                    coffersTargets = new (UpgradePurchase, int)[0]                               },
            new { id = "connections_l1", purseTargets = new[] { (UpgradePurchase.Connections, 1) },                  coffersTargets = new (UpgradePurchase, int)[0]                               },
            new { id = "town_invest_l1", purseTargets = new (UpgradePurchase, int)[0],                               coffersTargets = new[] { (UpgradePurchase.TownInvestment, 1) }              },
            new { id = "route_impr_l1",  purseTargets = new (UpgradePurchase, int)[0],                               coffersTargets = new[] { (UpgradePurchase.RouteImprovement, 1) }            },
            new { id = "all_l1",         purseTargets = new[] { (UpgradePurchase.Collection, 1), (UpgradePurchase.HeatDecay, 1), (UpgradePurchase.Connections, 1) },
                                         coffersTargets = new[] { (UpgradePurchase.TownInvestment, 1), (UpgradePurchase.RouteImprovement, 1) }                                                   },
        })
        {
            var pt = spec.purseTargets;
            var ct = spec.coffersTargets;
            upgSweep.Configs.Add(new ConfigEntry
            {
                Id = spec.id,
                Config = new SimConfig(),
                CreateStrategy = () =>
                {
                    bool inCrimePhase = false;
                    return state =>
                    {
                        if (!inCrimePhase && state.Purse >= upgSetupNeeded) inCrimePhase = true;

                        int delta = 0;
                        if (inCrimePhase && state.OrganizedCrimeLevel < 2 && state.Purse >= upgSetupCost)
                            delta = 1;

                        GameCore.Events.EventOption choice = GameCore.Events.EventOption.None;
                        if (state.PendingEvent != null) choice = GameCore.Events.EventOption.OptionA;

                        // Buy first affordable purse upgrade still below its level cap
                        UpgradePurchase upgrade = UpgradePurchase.None;
                        if (inCrimePhase)
                        {
                            foreach (var tup in pt)
                            {
                                int cur = UpgradeLevel(state, tup.Item1);
                                if (cur >= tup.Item2) continue;
                                float cost = UpgradeCost(tup.Item1, cur);
                                if (state.Purse >= cost + upgSetupCost) { upgrade = tup.Item1; break; }
                            }
                        }
                        // Coffers upgrades: buy whenever coffers buffer allows (any phase)
                        if (upgrade == UpgradePurchase.None)
                        {
                            foreach (var tup in ct)
                            {
                                int cur = UpgradeLevel(state, tup.Item1);
                                if (cur >= tup.Item2) continue;
                                float cost = UpgradeCost(tup.Item1, cur);
                                if (state.Coffers >= cost + coffersBuf) { upgrade = tup.Item1; break; }
                            }
                        }

                        return new PlayerInput
                        {
                            TaxRate                  = 0.20f,
                            SkimFraction             = inCrimePhase ? 0.10f : 0.40f,
                            OrganizedCrimeLevelDelta = delta,
                            BribeAmount              = inCrimePhase ? 5f : 0f,
                            EventChoice              = choice,
                            Upgrade                  = upgrade,
                        };
                    };
                },
            });
        }
        sweeps.Add(upgSweep);

        // §6.2 Legitimacy buffer — does LegitimacyHeatBufferPerCoffersUnit magnitude matter,
        // and does it change the optimal crime-phase skim fraction?
        // 3 buffer values × 3 skim fractions = 8 configs (no_buffer only tests 2 skims).
        var legitSweep = new SweepSpec { Name = "legitimacy_buffer", SeedCount = 20, MaxTicks = 200 };

        foreach (var spec in new[]
        {
            // No buffer baseline — two skim points to establish counterfactual
            new { id = "no_buf_skim10",  buffer = 0.000f, crimePhaseSkimFrac = 0.10f },
            new { id = "no_buf_skim30",  buffer = 0.000f, crimePhaseSkimFrac = 0.30f },
            // Current default buffer (0.005) — three skim fractions
            new { id = "cur_buf_skim05", buffer = 0.005f, crimePhaseSkimFrac = 0.05f },
            new { id = "cur_buf_skim10", buffer = 0.005f, crimePhaseSkimFrac = 0.10f },
            new { id = "cur_buf_skim30", buffer = 0.005f, crimePhaseSkimFrac = 0.30f },
            // Strong buffer (0.015) — three skim fractions
            new { id = "str_buf_skim05", buffer = 0.015f, crimePhaseSkimFrac = 0.05f },
            new { id = "str_buf_skim10", buffer = 0.015f, crimePhaseSkimFrac = 0.10f },
            new { id = "str_buf_skim30", buffer = 0.015f, crimePhaseSkimFrac = 0.30f },
        })
        {
            float bufferVal   = spec.buffer;
            float crimeSkimFrac = spec.crimePhaseSkimFrac;
            float legitSetupCost   = new SimConfig().OrganizedCrimeSetupCostPerLevel;
            float legitSetupNeeded = 2 * legitSetupCost;

            legitSweep.Configs.Add(new ConfigEntry
            {
                Id = spec.id,
                Config = new SimConfig { LegitimacyHeatBufferPerCoffersUnit = bufferVal },
                CreateStrategy = () =>
                {
                    bool inCrimePhase = false;
                    return state =>
                    {
                        if (!inCrimePhase && state.Purse >= legitSetupNeeded)
                            inCrimePhase = true;

                        int delta = 0;
                        if (inCrimePhase && state.OrganizedCrimeLevel < 2 && state.Purse >= legitSetupCost)
                            delta = 1;

                        GameCore.Events.EventOption choice = GameCore.Events.EventOption.None;
                        if (state.PendingEvent != null)
                            choice = GameCore.Events.EventOption.OptionA;

                        return new PlayerInput
                        {
                            TaxRate                  = 0.20f,
                            SkimFraction             = inCrimePhase ? crimeSkimFrac : 0.40f,
                            OrganizedCrimeLevelDelta = delta,
                            BribeAmount              = inCrimePhase ? 5f : 0f,
                            EventChoice              = choice,
                        };
                    };
                },
            });
        }
        sweeps.Add(legitSweep);

        // §competitor — rival competition: does adding rivals break win-rate balance?
        // Which counter-strategies work when rivals grow?
        // 6 configs × 20 seeds × 200 ticks
        var rivalSweep = new SweepSpec { Name = "rival_competition", SeedCount = 20, MaxTicks = 200 };

        float rivalSetupCost    = new SimConfig().OrganizedCrimeSetupCostPerLevel;
        float rivalSetupNeeded  = 2 * rivalSetupCost;
        float rivalCoffersBuf   = new SimConfig().TributePerTick * 8f;
        float rivalRouteCostBase= new SimConfig().UpgradeRouteImprovementCostBase;

        foreach (var spec in new[]
        {
            // baseline: rivals off (should reproduce ~70% from organized_crime sweep)
            new { id = "no_rivals_org2",     enableRivals = false, taxRate = 0.20f, gainRate = 0.006f, buyRoute = false },
            // default rivals, same org2 strategy — does competition hurt win rate?
            new { id = "rivals_org2",        enableRivals = true,  taxRate = 0.20f, gainRate = 0.006f, buyRoute = false },
            // lower tax to pull traffic from rivals
            new { id = "rivals_tax15_org2",  enableRivals = true,  taxRate = 0.15f, gainRate = 0.006f, buyRoute = false },
            // higher tax — does it cost traffic share badly when rivals are competing?
            new { id = "rivals_tax25_org2",  enableRivals = true,  taxRate = 0.25f, gainRate = 0.006f, buyRoute = false },
            // aggressive rivals (faster quality growth) — stress-test the default config
            new { id = "rivals_strong_org2", enableRivals = true,  taxRate = 0.20f, gainRate = 0.015f, buyRoute = false },
            // counter rivals with a Route Improvement upgrade (boosts competitive attractiveness)
            new { id = "rivals_route_org2",  enableRivals = true,  taxRate = 0.20f, gainRate = 0.006f, buyRoute = true  },
        })
        {
            bool  enableRivals = spec.enableRivals;
            float taxRate      = spec.taxRate;
            float gainRate     = spec.gainRate;
            bool  buyRoute     = spec.buyRoute;

            rivalSweep.Configs.Add(new ConfigEntry
            {
                Id = spec.id,
                Config = new SimConfig
                {
                    EnableRivals         = enableRivals,
                    RivalQualityGainRate = gainRate,
                },
                CreateStrategy = () =>
                {
                    bool inCrimePhase   = false;
                    bool routePurchased = false;
                    return state =>
                    {
                        if (!inCrimePhase && state.Purse >= rivalSetupNeeded)
                            inCrimePhase = true;

                        int delta = 0;
                        if (inCrimePhase && state.OrganizedCrimeLevel < 2 && state.Purse >= rivalSetupCost)
                            delta = 1;

                        GameCore.Events.EventOption choice = GameCore.Events.EventOption.None;
                        if (state.PendingEvent != null)
                            choice = GameCore.Events.EventOption.OptionA;

                        UpgradePurchase upgrade = UpgradePurchase.None;
                        if (buyRoute && !routePurchased && state.RouteImprovementLevel < 1
                            && state.Coffers >= rivalRouteCostBase + rivalCoffersBuf)
                        {
                            upgrade        = UpgradePurchase.RouteImprovement;
                            routePurchased = true;
                        }

                        return new PlayerInput
                        {
                            TaxRate                  = taxRate,
                            SkimFraction             = inCrimePhase ? 0.10f : 0.40f,
                            OrganizedCrimeLevelDelta = delta,
                            BribeAmount              = inCrimePhase ? 5f : 0f,
                            EventChoice              = choice,
                            Upgrade                  = upgrade,
                        };
                    };
                },
            });
        }
        sweeps.Add(rivalSweep);

        // §incursion_pressure — does scaling RivalIncursionChance with rival share
        // create runaway event debt in late-game dominated scenarios?
        // Baseline (no rivals) vs default pressure vs high pressure scale vs counter-strategies.
        // All configs use org2+pay_all (known 70% baseline) so event differences are isolated.
        var incSweep = new SweepSpec { Name = "incursion_pressure", SeedCount = 20, MaxTicks = 200 };

        float incSetupCost   = new SimConfig().OrganizedCrimeSetupCostPerLevel;
        float incSetupNeeded = 2 * incSetupCost;
        float incCoffersBuf  = new SimConfig().TributePerTick * 8f;
        float incRouteCost   = new SimConfig().UpgradeRouteImprovementCostBase;

        foreach (var spec in new[]
        {
            // Flat 10% baseline — no rivals, no pressure scaling
            new { id = "no_rivals_flat10",    enableRivals = false, pressureScale = 0.30f, taxRate = 0.20f, gainRate = 0.006f, buyRoute = false },
            // Default rivals, default pressure (0.30/share)
            new { id = "rivals_pressure_def", enableRivals = true,  pressureScale = 0.30f, taxRate = 0.20f, gainRate = 0.006f, buyRoute = false },
            // Aggressive rivals (faster quality growth) — dominant share reached sooner
            new { id = "rivals_pressure_str", enableRivals = true,  pressureScale = 0.30f, taxRate = 0.20f, gainRate = 0.015f, buyRoute = false },
            // 2× pressure scale stress test — is this too punishing?
            new { id = "rivals_pressure_2x",  enableRivals = true,  pressureScale = 0.60f, taxRate = 0.20f, gainRate = 0.006f, buyRoute = false },
            // Lower tax pulls share back from rivals, reducing incursion pressure
            new { id = "rivals_tax15_press",  enableRivals = true,  pressureScale = 0.30f, taxRate = 0.15f, gainRate = 0.006f, buyRoute = false },
            // Route upgrade counter: maintains competitive attractiveness
            new { id = "rivals_route_press",  enableRivals = true,  pressureScale = 0.30f, taxRate = 0.20f, gainRate = 0.006f, buyRoute = true  },
        })
        {
            bool  er    = spec.enableRivals;
            float ps    = spec.pressureScale;
            float tr    = spec.taxRate;
            float gr    = spec.gainRate;
            bool  route = spec.buyRoute;

            incSweep.Configs.Add(new ConfigEntry
            {
                Id = spec.id,
                Config = new SimConfig
                {
                    EnableRivals                        = er,
                    RivalIncursionPressurePerSharePoint = ps,
                    RivalQualityGainRate                = gr,
                },
                CreateStrategy = () =>
                {
                    bool inCrimePhase   = false;
                    bool routePurchased = false;
                    return state =>
                    {
                        if (!inCrimePhase && state.Purse >= incSetupNeeded)
                            inCrimePhase = true;

                        int delta = 0;
                        if (inCrimePhase && state.OrganizedCrimeLevel < 2 && state.Purse >= incSetupCost)
                            delta = 1;

                        GameCore.Events.EventOption choice = GameCore.Events.EventOption.None;
                        if (state.PendingEvent != null)
                            choice = GameCore.Events.EventOption.OptionA;

                        UpgradePurchase upgrade = UpgradePurchase.None;
                        if (route && !routePurchased && state.RouteImprovementLevel < 1
                            && state.Coffers >= incRouteCost + incCoffersBuf)
                        {
                            upgrade        = UpgradePurchase.RouteImprovement;
                            routePurchased = true;
                        }

                        return new PlayerInput
                        {
                            TaxRate                  = tr,
                            SkimFraction             = inCrimePhase ? 0.10f : 0.40f,
                            OrganizedCrimeLevelDelta = delta,
                            BribeAmount              = inCrimePhase ? 5f : 0f,
                            EventChoice              = choice,
                            Upgrade                  = upgrade,
                        };
                    };
                },
            });
        }
        sweeps.Add(incSweep);

        // §rival_events — TradeDelegation + DivertedCaravan combined balance validation.
        // Both events are active by default. Tests all 4 org strategies with pay_all response.
        // Goal: confirm combined win rate stays 55–70% across strategies; no event stacking cliff.
        // 4 configs × 20 seeds × 200 ticks
        var revSweep = new SweepSpec { Name = "rival_events", SeedCount = 20, MaxTicks = 200 };

        float revSetupCost = new SimConfig().OrganizedCrimeSetupCostPerLevel;

        foreach (var spec in new[]
        {
            new { id = "pure_skim",  orgTarget = 0, bribe = 0f },
            new { id = "org1_pay",   orgTarget = 1, bribe = 3f },
            new { id = "org2_pay",   orgTarget = 2, bribe = 5f },
            new { id = "org3_pay",   orgTarget = 3, bribe = 7f },
        })
        {
            int   orgTarget   = spec.orgTarget;
            float bribeAmt    = spec.bribe;
            float setupNeeded = orgTarget * revSetupCost;

            revSweep.Configs.Add(new ConfigEntry
            {
                Id     = spec.id,
                Config = new SimConfig(),   // full defaults: rivals on, both events active
                CreateStrategy = () =>
                {
                    bool inCrimePhase = orgTarget == 0;   // pure_skim starts in crime phase
                    return state =>
                    {
                        if (!inCrimePhase && state.Purse >= setupNeeded)
                            inCrimePhase = true;

                        int delta = 0;
                        if (inCrimePhase && state.OrganizedCrimeLevel < orgTarget
                            && state.Purse >= revSetupCost)
                            delta = 1;

                        GameCore.Events.EventOption choice = GameCore.Events.EventOption.None;
                        if (state.PendingEvent != null)
                            choice = GameCore.Events.EventOption.OptionA;

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
        sweeps.Add(revSweep);

        // §difficulty_presets — validate Easy / Normal / Hard win rate targets.
        // Strategy: org2+pay_all (recommended Normal entry path) across all difficulties.
        // Also: org1 on Easy (predicted viable ~85%), org3 on Hard (skilled ceiling).
        // 5 configs × 20 seeds × 200 ticks
        var diffSweep = new SweepSpec { Name = "difficulty_presets", SeedCount = 20, MaxTicks = 200 };

        float diffSetupCost = new SimConfig().OrganizedCrimeSetupCostPerLevel;

        foreach (var spec in new[]
        {
            new { id = "easy_org1",   difficulty = 0, orgTarget = 1, bribe = 3f },
            new { id = "easy_org2",   difficulty = 0, orgTarget = 2, bribe = 5f },
            new { id = "normal_org2", difficulty = 1, orgTarget = 2, bribe = 5f },
            new { id = "hard_org2",   difficulty = 2, orgTarget = 2, bribe = 5f },
            new { id = "hard_org3",   difficulty = 2, orgTarget = 3, bribe = 7f },
        })
        {
            int   orgTarget   = spec.orgTarget;
            float bribeAmt    = spec.bribe;
            float setupNeeded = orgTarget * diffSetupCost;

            SimConfig cfg;
            switch (spec.difficulty)
            {
                case 0:
                    cfg = new SimConfig
                    {
                        RivalIncursionChance                = 0.05f,
                        RivalIncursionPressurePerSharePoint = 0.12f,
                        RivalIncursionTributeCost           = 30f,
                        WealthWinThreshold                  = 2500f,
                        AuditThreshold                      = 88f,
                        RivalQualityGainRate                = 0.003f,
                        TributePerTick                      = 4f,
                        InspectorVisitChance                = 0.03f,
                    };
                    break;
                case 2:
                    cfg = new SimConfig
                    {
                        RivalIncursionChance                = 0.14f,
                        RivalIncursionPressurePerSharePoint = 0.50f,
                        WealthWinThreshold                  = 5000f,
                        AuditThreshold                      = 60f,
                        RivalQualityGainRate                = 0.012f,
                        TributePerTick                      = 9f,
                        MerchantComplaintChance             = 0.18f,
                        InspectorVisitChance                = 0.08f,
                    };
                    break;
                default:
                    cfg = new SimConfig();
                    break;
            }

            diffSweep.Configs.Add(new ConfigEntry
            {
                Id     = spec.id,
                Config = cfg,
                CreateStrategy = () =>
                {
                    bool inCrimePhase = orgTarget == 0;
                    return state =>
                    {
                        if (!inCrimePhase && state.Purse >= setupNeeded)
                            inCrimePhase = true;

                        int delta = 0;
                        if (inCrimePhase && state.OrganizedCrimeLevel < orgTarget
                            && state.Purse >= diffSetupCost)
                            delta = 1;

                        GameCore.Events.EventOption choice = GameCore.Events.EventOption.None;
                        if (state.PendingEvent != null)
                            choice = GameCore.Events.EventOption.OptionA;

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
        sweeps.Add(diffSweep);

        return sweeps;
    }

    static int UpgradeLevel(WorldState s, UpgradePurchase upg)
    {
        if (upg == UpgradePurchase.Collection)       return s.CollectionUpgradeLevel;
        if (upg == UpgradePurchase.HeatDecay)        return s.HeatDecayUpgradeLevel;
        if (upg == UpgradePurchase.Connections)      return s.ConnectionsLevel;
        if (upg == UpgradePurchase.TownInvestment)   return s.TownInvestmentLevel;
        if (upg == UpgradePurchase.RouteImprovement) return s.RouteImprovementLevel;
        return 0;
    }

    static float UpgradeCost(UpgradePurchase upg, int currentLevel)
    {
        var cfg = new SimConfig();
        if (upg == UpgradePurchase.Collection)       return cfg.UpgradeCollectionCostBase       * Mathf.Pow(cfg.UpgradeCollectionCostScalePerLevel,       currentLevel);
        if (upg == UpgradePurchase.HeatDecay)        return cfg.UpgradeHeatDecayCostBase        * Mathf.Pow(cfg.UpgradeHeatDecayCostScalePerLevel,        currentLevel);
        if (upg == UpgradePurchase.Connections)      return cfg.UpgradeConnectionsCostBase      * Mathf.Pow(cfg.UpgradeConnectionsCostScalePerLevel,      currentLevel);
        if (upg == UpgradePurchase.TownInvestment)   return cfg.UpgradeTownInvestmentCostBase   * Mathf.Pow(cfg.UpgradeTownInvestmentCostScalePerLevel,   currentLevel);
        if (upg == UpgradePurchase.RouteImprovement) return cfg.UpgradeRouteImprovementCostBase * Mathf.Pow(cfg.UpgradeRouteImprovementCostScalePerLevel, currentLevel);
        return float.MaxValue;
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
        int   totalEvents              = 0;
        int   totalRivalIncursions     = 0;
        int   totalTradeDelegations    = 0;
        int   totalDivertedCaravans    = 0;
        float totalShare               = 0f;
        float minShare        = 1f;
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
            if (row.EventFired == GameCore.Events.EventType.RivalIncursion)
                totalRivalIncursions++;
            if (row.EventFired == GameCore.Events.EventType.TradeDelegation)
                totalTradeDelegations++;
            if (row.EventFired == GameCore.Events.EventType.DivertedCaravan)
                totalDivertedCaravans++;
            totalShare += row.PlayerTrafficShare;
            if (row.PlayerTrafficShare < minShare) minShare = row.PlayerTrafficShare;
        }

        int n = sim.Telemetry.Count;
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
            TotalEventsFired              = totalEvents,
            TotalRivalIncursionsFired     = totalRivalIncursions,
            TotalTradeDelegationsFired    = totalTradeDelegations,
            TotalDivertedCaravansFired    = totalDivertedCaravans,
            MeanPlayerTrafficShare     = n > 0 ? totalShare / n : 1f,
            MinPlayerTrafficShare  = n > 0 ? minShare   : 1f,
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
            "total_skimmed,total_coffers_contribution,wealth_win," +
            "rival_incursions_fired,trade_delegations_fired,diverted_caravans_fired," +
            "mean_player_traffic_share,min_player_traffic_share");

        foreach (var r in summaries)
            sb.AppendLine(
                $"{r.SweepName},{r.ConfigId},{r.Seed}," +
                $"{r.FinalTick},{r.FinalPurse:F2},{r.PeakPurse:F2}," +
                $"{r.FinalHeat:F2},{r.PeakHeat:F2},{r.FirstHeat75Tick}," +
                $"{r.FinalTownQuality:F3},{r.FinalSafety:F3},{r.EndReason}," +
                $"{r.TotalSkimmed:F2},{r.TotalCoffersContribution:F2}," +
                $"{(r.EndReason == EndReason.WealthWin ? 1 : 0)}," +
                $"{r.TotalRivalIncursionsFired},{r.TotalTradeDelegationsFired},{r.TotalDivertedCaravansFired}," +
                $"{r.MeanPlayerTrafficShare:F3},{r.MinPlayerTrafficShare:F3}");

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
            "mean_skim_to_coffers_ratio,mean_events_fired,mean_rival_incursions," +
            "mean_trade_delegations,mean_diverted_caravans," +
            "mean_player_traffic_share,min_player_traffic_share");

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
            float meanEventsFired          = Avg(runs, r => r.TotalEventsFired);
            float meanRivalIncursions      = Avg(runs, r => r.TotalRivalIncursionsFired);
            float meanTradeDelegations     = Avg(runs, r => r.TotalTradeDelegationsFired);
            float meanDivertedCaravans     = Avg(runs, r => r.TotalDivertedCaravansFired);
            float meanTrafficShare         = Avg(runs, r => r.MeanPlayerTrafficShare);
            float minTrafficShare        = Avg(runs, r => r.MinPlayerTrafficShare);
            // pocket-vs-coffers ratio: >1 means more skimmed than invested in town
            float skimToCoffersRatio = meanTotalCoffers > 0f ? meanTotalSkim / meanTotalCoffers : 0f;

            sb.AppendLine(
                $"{sweepName},{cfgId},{n}," +
                $"{meanFinalTick:F1},{meanFinalPurse:F1},{meanPeakPurse:F1}," +
                $"{wealthWinPct:F1},{auditPct:F1},{rivalPct:F1}," +
                $"{meanPeakHeat:F1},{meanFirstHeat75:F1}," +
                $"{meanFinalSafety:F3},{meanTotalSkim:F1},{meanTotalCoffers:F1}," +
                $"{skimToCoffersRatio:F3},{meanEventsFired:F1},{meanRivalIncursions:F1}," +
                $"{meanTradeDelegations:F1},{meanDivertedCaravans:F1}," +
                $"{meanTrafficShare:F3},{minTrafficShare:F3}");
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
        public int    TotalRivalIncursionsFired;
        public int    TotalTradeDelegationsFired;
        public int    TotalDivertedCaravansFired;
        public float  MeanPlayerTrafficShare;
        public float  MinPlayerTrafficShare;
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
