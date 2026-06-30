# CaravanTrails — Agent Implementation TODO

This document is a prioritised task list for an agentic coding agent. Each task
includes the files to read first, the files to modify or create, what the
expected behaviour is, and a testability note. Work through tasks in order;
later tasks depend on earlier ones.

Read the following files before starting any task so you understand the
architecture:
- `Assets/Scripts/GameCore/Sim/Simulator.cs` — 13-step tick loop
- `Assets/Scripts/GameCore/Sim/WorldState.cs` — player/town state schema
- `Assets/Scripts/GameCore/Sim/RivalTownState.cs` — rival state schema
- `Assets/Scripts/GameCore/Sim/RivalTownAI.cs` — current (thin) rival AI
- `Assets/Scripts/GameCore/Economy/TrafficModel.cs` — attractiveness formula
- `Assets/Scripts/GameCore/Events/DefaultEventModel.cs` — event firing/resolution
- `Assets/Tests/CoreTests/GoldenRunTests.cs` — golden-run hash (update it after
  any balance change)

---

## TASK 1 — Goal-Directed Rival AI

**Why:** Rivals currently drift toward a fixed tax baseline with noise. They do
not pursue a win condition, react to the player, or invest in upgrades. This is
the single most important gap given the game's competitive framing.

**Read first:**
- `Assets/Scripts/GameCore/Sim/RivalTownAI.cs`
- `Assets/Scripts/GameCore/Sim/RivalTownState.cs`
- `Assets/Scripts/GameCore/Economy/TrafficModel.cs`

**What to implement:**

1. **Goal state machine.** Add a `RivalGoal` enum to `RivalTownState`:
   `GrowTraffic`, `AccumulateWealth`, `ImproveInfrastructure`, `SuppressThreat`.
   Each rival has one active goal at a time. Goal switches on a condition check
   every 10 ticks.

2. **Goal selection logic** in `RivalTownAI.cs`:
   - `GrowTraffic` when the rival's traffic share drops below 18 %.
   - `AccumulateWealth` when traffic share is healthy (> 22 %) and quality > 0.5.
   - `ImproveInfrastructure` when town quality < 0.4 or safety < 0.4.
   - `SuppressThreat` when the *player's* traffic share exceeds 30 % for two
     consecutive goal-check intervals.

3. **Per-goal behaviour** (replaces current drift logic):
   - `GrowTraffic`: cut tax rate toward 0.08 over 3 ticks; boost quality
     spending.
   - `AccumulateWealth`: raise tax rate toward personality baseline + 0.05;
     invest in a virtual "upgrade" that adds a flat 0.04 attractiveness bonus
     (costs 80 units from a `RivalWealth` counter, resets cost each level).
   - `ImproveInfrastructure`: spend `RivalWealth` to recover quality at 2×
     normal rate for 5 ticks.
   - `SuppressThreat`: temporarily undercut the player's current tax rate by
     0.03 and add a 0.06 attractiveness spike for 4 ticks (models aggressive
     lobbying / road improvement).

4. **`RivalWealth` accumulation.** Each tick, rival earns
   `traffic_share × tax_rate × 120` units into a `RivalWealth` float on
   `RivalTownState`. This does not affect player gameplay directly but enables
   investment decisions.

5. **Personality differentiation.** Give each of the four rivals a named
   personality profile (store as a struct on `RivalTownState`):
   - `Westport`: aggressive tax cutter, prefers `GrowTraffic`.
   - `Millhaven`: infrastructure investor, prefers `ImproveInfrastructure`.
   - `Eastgate`: wealth accumulator, prefers `AccumulateWealth`.
   - `Southford`: reactive, switches to `SuppressThreat` quickly.
   Personality biases the goal-selection thresholds (e.g. Southford triggers
   `SuppressThreat` at player share > 24 % instead of 30 %).

**Testability:** Add `RivalAITests.cs` in `Assets/Tests/CoreTests/`. Verify:
- A rival in `GrowTraffic` mode reduces its tax rate each tick.
- `SuppressThreat` triggers when player share exceeds threshold.
- `RivalWealth` accumulates proportionally to traffic and tax rate.
- Determinism: same seed produces identical rival goal sequences.

Update the golden-run hash in `GoldenRunTests.cs` after this change.

---

## TASK 2 — Rival Memory & Player-Awareness

**Why:** Rivals should notice what the player is doing (high crime, aggressive
skim, low tax) and react meaningfully, making the competition feel intelligent.

**Read first:**
- `Assets/Scripts/GameCore/Sim/WorldState.cs` (player heat, crime level, tax)
- `Assets/Scripts/GameCore/Sim/RivalTownState.cs`
- `Assets/Scripts/GameCore/Sim/Simulator.cs` (step 2, rival update)

**What to implement:**

1. **Observation window.** Add a `RivalMemory` struct to `RivalTownState` with:
   - `int TicksPlayerHighHeat` — incremented each tick player heat > 60.
   - `int TicksPlayerDominant` — incremented each tick player share > 28 %.
   - `float ObservedPlayerTaxRate` — rolling 5-tick average of player tax.
   Both counters reset when the condition is no longer true.

2. **Reactive events.** In the Simulator's rival-update step, after goal
   selection, fire one of these reactions (maximum once per rival per 15 ticks):
   - If `TicksPlayerHighHeat >= 5`: rival sends an anonymous tip, adding +8 heat
     to the player (`WorldState.Heat += 8`). Log as `RivalAction.Tip`.
   - If `TicksPlayerDominant >= 8`: rival triggers a `RivalIncursion` event (if
     not already pending) as if the player's organised crime level were 1.
   - If `ObservedPlayerTaxRate < 0.07` for 5 ticks: rival matches the low rate
     (floor their tax at `ObservedPlayerTaxRate + 0.01`) for 6 ticks.

3. **Expose memory in telemetry.** Add `RivalMemory` fields to the tick
   telemetry CSV logged by `SimHarness`.

**Testability:** Add tests to `RivalAITests.cs`:
- Anonymous tip fires only when heat threshold sustained for correct duration.
- Tip is not fired more than once per 15-tick cooldown.
- Tax-matching behaviour tracks player correctly.

---

## TASK 3 — Commodity Abstraction (Trade Goods Layer)

**Why:** All trade is currently "traffic volume" — a single number. Adding named
commodity types makes events richer, gives rivals distinct identities, and
creates new strategic levers without breaking the existing traffic model.

**Read first:**
- `Assets/Scripts/GameCore/Economy/TrafficModel.cs`
- `Assets/Scripts/GameCore/Sim/WorldState.cs`
- `Assets/Scripts/GameCore/Events/DefaultEventModel.cs`

**What to implement:**

1. **`CommodityType` enum** (new file
   `Assets/Scripts/GameCore/Economy/CommodityType.cs`):
   `Grain`, `Silk`, `Spice`, `Timber`, `Livestock`. Five types only.

2. **`TradeGoodsProfile`** (new file
   `Assets/Scripts/GameCore/Economy/TradeGoodsProfile.cs`):
   - A fixed weight per commodity type that sums to 1.0 (e.g. Grain 0.30, Silk
     0.25, Spice 0.20, Timber 0.15, Livestock 0.10).
   - A `TaxSensitivity` per commodity (Silk highest at 1.4×, Grain lowest at
     0.6×) that multiplies into the tax elasticity term in `TrafficModel`.
   - Seasonal modifier: Grain +20 % in ticks 20–30 (harvest window), Livestock
     +15 % in ticks 60–70. Apply inside `TrafficModel.ComputeAttractiveness`.

3. **Dominant good.** Each tick, compute which commodity type contributes the
   most traffic. Store as `WorldState.DominantCommodity`. Expose in UI status
   display (text label only, e.g. "Primary trade: Silk").

4. **Commodity-sensitive events.** Modify two existing events in
   `DefaultEventModel.cs`:
   - `MerchantComplaint`: if dominant good is Silk, complaint cost option is
     −55 (luxury merchants are demanding); if Grain, it is −30.
   - `DivertedCaravan`: flavour text references the dominant good by name.

5. **No new sliders.** The commodity layer is entirely implicit — the player
   sees its effects through event costs and traffic variation, not direct
   controls. Do not add UI controls for commodities.

**Testability:** Add `CommodityTests.cs`:
- Seasonal modifiers activate and deactivate at correct tick boundaries.
- Tax sensitivity causes Silk traffic to drop faster than Grain when tax rises.
- `DominantCommodity` is deterministic for a given seed.

---

## TASK 4 — Upgrade System for Rivals

**Why:** The player can invest in 5 upgrade types. Rivals have no equivalent,
making the late game one-sided once the player is upgraded.

**Read first:**
- `Assets/Scripts/GameCore/Sim/RivalTownState.cs`
- `Assets/Scripts/GameCore/Sim/RivalTownAI.cs` (post Task 1)
- `Assets/Scripts/GameCore/Economy/TrafficModel.cs`

**What to implement:**

1. **`RivalUpgrades` struct** on `RivalTownState`:
   - `int RouteLevel` — flat attractiveness bonus (0.05 per level, max 3).
   - `int QualityLevel` — multiplies quality recovery rate (1.2× per level, max 2).
   - `int SafetyLevel` — flat safety recovery bonus (0.02 per level, max 2).

2. **Investment logic** in `RivalTownAI.cs`:
   - When `RivalWealth >= UpgradeCost` and goal is `ImproveInfrastructure` or
     `AccumulateWealth`, spend wealth to increment the most deficient upgrade.
   - Base costs: RouteLevel 100, QualityLevel 80, SafetyLevel 90. Each level
     multiplies cost by 1.5×.
   - Rivals invest at most one upgrade per 12 ticks.

3. **Apply upgrades** in `TrafficModel.ComputeRivalAttractiveness`:
   - Add `rival.Upgrades.RouteLevel × 0.05` to flat attractiveness.
   - Multiply quality recovery by `1 + rival.Upgrades.QualityLevel × 0.2`.

4. **World map display.** In the world map panel, show a small upgrade indicator
   per rival (e.g. "Route ★★☆" using the existing TextMesh Pro labels). Read
   `RivalTownState.Upgrades` from `GameController`.

**Testability:** Add tests:
- Rival with `AccumulateWealth` goal and sufficient wealth purchases an upgrade
  within 12 ticks.
- Attractiveness increases correctly per upgrade level.
- Upgrade purchase deducts the correct cost from `RivalWealth`.

---

## TASK 5 — Agent-Based Caravan Simulation

**Why:** Caravans are currently visual-only animations driven by aggregate
traffic volume. The design notes (§6.5) flag agent-based implementation as the
intended replacement. This makes traffic legible and lets individual caravans
carry commodity type, origin rival town, and destination.

**Read first:**
- `Assets/Scripts/Unity/Presentation/CaravanManager.cs`
- `Assets/Scripts/GameCore/Economy/TrafficModel.cs`
- `Assets/Scripts/GameCore/Sim/WorldState.cs`
- `Assets/Scripts/GameCore/Economy/CommodityType.cs` (created in Task 3)

**What to implement:**

1. **`CaravanAgent` data class** (new file
   `Assets/Scripts/GameCore/Sim/CaravanAgent.cs`):
   ```
   int Id
   CommodityType Commodity
   string OriginTown        // "Westport" | "Millhaven" | etc. | "Random"
   float TaxPaid
   bool Diverted            // true if rerouted by a DivertedCaravan event
   int SpawnTick
   ```
   This is a pure-data struct; no Unity dependency.

2. **`CaravanSpawner`** in the simulator (add to `Simulator.cs` tick step 3,
   after traffic calculation):
   - Each tick, spawn `N` caravan agents where `N = Round(traffic_volume × 0.8)`
     clamped to [0, 8].
   - Assign `OriginTown` weighted by each rival's traffic share.
   - Assign `Commodity` weighted by `TradeGoodsProfile` (Task 3).
   - Set `TaxPaid = tax_rate × commodity_base_value` (base values: Grain 10,
     Silk 25, Spice 20, Timber 12, Livestock 15).
   - Store spawned agents in `WorldState.ActiveCaravans` (a `List<CaravanAgent>`
     cleared each tick after presentation reads it).

3. **Wire to `CaravanManager`**: In `CaravanManager.Update`, read
   `WorldState.ActiveCaravans` count instead of the current traffic-volume
   heuristic to decide how many Unity caravan GameObjects to activate from the
   pool. Pass `Commodity` type to the caravan's material/colour tint (one colour
   per commodity, defined as a static lookup — keep it subtle).

4. **Tooltip on hover.** When the player's cursor is over a caravan
   GameObject, show a small TextMesh Pro tooltip with: commodity name, origin
   town, tax paid. Use a world-space canvas parented to the caravan. Hide on
   exit.

5. **Diverted caravans.** When a `DivertedCaravan` event fires and the player
   chooses to accept the loss, mark the next 3 spawned caravans as
   `Diverted = true` and reduce their `TaxPaid` by 60 %. The visual tint on
   diverted caravans uses a desaturated grey.

**Testability:**
- Spawn count matches `Round(traffic × 0.8)` clamped correctly.
- Origin town distribution matches rival share weights over 100 ticks.
- Diverted flag and reduced tax propagate correctly through event resolution.
- No Unity types in `CaravanAgent` or spawner logic (keep sim-layer pure).

---

## TASK 6 — Expanded Event System

**Why:** Events are currently 8 types with binary choices. The rival AI additions
(Tasks 1–2) create new conditions that should surface as events. Commodity types
(Task 3) allow flavour-rich trade events.

**Read first:**
- `Assets/Scripts/GameCore/Events/DefaultEventModel.cs`
- `Assets/Scripts/GameCore/Events/EventType.cs`
- `Assets/Scripts/GameCore/Sim/WorldState.cs`

**What to implement:**

1. **Add 5 new `EventType` values:**
   - `RivalUndercutting` — a named rival has dropped tax below the player's
     rate by more than 0.06 for 3 consecutive ticks.
   - `CommodityShortage` — dominant commodity traffic drops > 25 % in one tick
     (seasonal edge or bad RNG roll).
   - `BanditRaid` — fires when safety < 0.25 (replaces the one-shot `BanditSurge`
     at tick 75 with a repeatable version; disable the tick-75 one-shot if this
     event is unlocked by safety dropping).
   - `RivalCollapse` — fires when any rival's quality drops below 0.10; player
     can poach their merchants (traffic bonus) or send aid (reputation bonus).
   - `GuildDemand` — fires at tick 35 (one-shot); a merchant guild demands a
     road-quality guarantee. Option A: pay 50 from coffers for +0.08 reputation.
     Option B: refuse, −0.06 reputation over 3 ticks.

2. **Event resolution handlers** for each new type in `DefaultEventModel.cs`,
   following the existing pattern:
   - `RivalUndercutting` Option A: match rival's rate for 4 ticks (auto-adjust
     `PlayerInput.TaxRate`); Option B: ignore (rival gains 2 % traffic).
   - `CommodityShortage` Option A: waive taxes on that commodity for 3 ticks
     (coffers hit −20); Option B: maintain rates (−4 % reputation).
   - `BanditRaid` Option A: emergency patrol (−40 purse, +6 % safety); Option B:
     curfew (−3 % town quality, −5 % reputation, +4 % safety).
   - `RivalCollapse` Option A: poach (+0.08 traffic attractiveness 6 ticks);
     Option B: aid (+0.05 reputation, rival recovers to quality 0.20).
   - `GuildDemand` as described above.

3. **Firing conditions** in `DefaultEventModel.FireNextEvent`:
   - `RivalUndercutting`: check rival tax history (requires Task 1's rival
     state). Fire at most once per rival per game.
   - `CommodityShortage`: check tick-over-tick dominant commodity delta.
   - `BanditRaid`: check `WorldState.Safety < 0.25`; cooldown 15 ticks.
   - `RivalCollapse`: check each `RivalTownState.Quality < 0.10`; fire once per
     rival.
   - `GuildDemand`: one-shot at tick 35 (same pattern as existing seasonal
     events).

4. **Flavour text.** Each new event needs 3 body-text variants (same pattern as
   existing events). Reference commodity names and rival town names by name where
   applicable.

**Testability:** Add `EventSystemTests.cs` or extend existing event tests:
- Each new event fires only under its trigger condition.
- Cooldowns and one-shot guards work correctly.
- Option resolution applies correct deltas to `WorldState`.
- Determinism holds across all new event paths.

---

## TASK 7 — Campaign Progression Layer

**Why:** The game is currently a single open-ended run with no arc. A lightweight
campaign structure raises stakes, creates pacing, and gives the rival AI a
long-term context to compete within.

**Read first:**
- `Assets/Scripts/GameCore/Sim/WorldState.cs`
- `Assets/Scripts/GameCore/Sim/Simulator.cs` (end conditions)
- `Assets/Scripts/GameCore/EndConditions/DefaultEndConditionEvaluator.cs`
- `Assets/Scripts/Unity/UI/GameController.cs`

**What to implement:**

1. **`Era` enum and era boundaries** (new file
   `Assets/Scripts/GameCore/Sim/Era.cs`):
   - `EarlyPosting` ticks 0–39
   - `EstablishedPrefect` ticks 40–79
   - `SeniorOfficial` ticks 80–119
   - `EndGame` ticks 120+
   Store `WorldState.CurrentEra`; update at the start of each tick.

2. **Per-era balance modifiers** applied in `Simulator.cs`:
   - `EarlyPosting`: tribute is 5/tick (reduced), rival starting quality 0.55.
   - `EstablishedPrefect`: tribute rises to 7/tick (current default), a second
     rival becomes goal-directed (Task 1 AI activates for Millhaven).
   - `SeniorOfficial`: all four rivals are fully goal-directed; heat accrual
     multiplier +10 %; a mandatory `GovernorAudit` event fires once.
   - `EndGame`: win threshold rises to 4 500 purse; rivals' `SuppressThreat`
     sensitivity doubles.

3. **Era transition narrative.** When an era boundary is crossed, push a
   non-blocking toast notification (reuse the tutorial avatar panel) with a
   single sentence describing what has changed (e.g. "Word of your success
   reaches the capital. Rivals grow bolder."). Do not block the tick.

4. **`GovernorAudit` event** (add to `EventType` enum):
   - Fires once at tick 85 (SeniorOfficial era).
   - Three-option event (extend the modal if needed, or use A/B with a
     descriptive B covering two sub-choices resolved by a second modal).
   - Option A: Full cooperation (−80 purse, −30 heat, +0.06 rep).
   - Option B: Partial disclosure (−40 purse, −10 heat, roll: 30 % chance
     +15 heat spike).
   - Option C (third button): Bribery (−120 purse, −40 heat, +0.04 rep).

5. **Campaign high-score.** Add `EraReached` and `FinalEra` to the rankings
   entry so the high-score ledger shows progression depth, not just final purse.

**Testability:**
- Era transitions occur at correct tick boundaries.
- Balance modifiers (tribute, rival count) apply exactly at transition.
- `GovernorAudit` fires only once, only in `SeniorOfficial` era.
- Rankings serialisation round-trips `EraReached` correctly.

---

## TASK 8 — TradeGraph Integration

**Why:** `TradeGraph.cs` and `TradeSimulator.cs` exist in the codebase but are
not wired into the active game. They model an inter-town trade network as a
graph. Integrating them unlocks route-level decision making (which roads to
invest in, which rivals to block).

**Read first:**
- `Assets/Scripts/GameCore/Economy/TradeGraph.cs`
- `Assets/Scripts/GameCore/Economy/TradeSimulator.cs`
- `Assets/Scripts/GameCore/Economy/TrafficModel.cs`
- `Assets/Scripts/GameCore/Sim/Simulator.cs`

**What to implement:**

1. **Audit `TradeGraph` and `TradeSimulator`** fully before writing any code.
   Document (in comments at the top of each file) what they currently do and
   what is broken or incomplete.

2. **Route nodes.** Model the five towns (player + 4 rivals) as nodes. Edges
   represent trade routes with a `Quality` float [0, 1] and `Bandits` bool.
   Initialise edge qualities from `WorldState.Safety` and rival safety values.

3. **Route Investment upgrade** (replaces or extends the existing Route
   Improvement upgrade in Task 4). Spending coffers on `RouteImprovement` now
   raises the edge quality on routes entering the player's town specifically
   (not a flat attractiveness bonus). `TrafficModel` reads edge quality when
   computing attractiveness for a given origin town.

4. **Bandit events on edges.** When `BanditRaid` fires (Task 6), randomly
   select one incoming edge and set `Bandits = true` for 6 ticks. While active,
   that edge reduces attractiveness by 0.12 for caravans originating from that
   rival's town. The `DivertedCaravan` event now correctly references which edge
   is blocked.

5. **World map visualisation.** Draw the trade graph edges on the world map
   panel. Use line renderers (or TextMesh Pro dashes as a fallback). Colour by
   edge quality: green (> 0.7), gold (0.4–0.7), red (< 0.4). Show a skull icon
   on edges with active bandits.

**Testability:**
- Edge quality updates propagate to attractiveness correctly.
- Bandit flag reduces attractiveness by exactly 0.12 for the correct origin.
- `TradeSimulator` produces deterministic results for a given seed.

---

## General Implementation Rules

- **Never break determinism.** Every new random draw must use the existing
  seeded `System.Random` instance passed through the sim, not `UnityEngine.Random`
  or `new System.Random()`.
- **No new sliders without prior approval.** Complexity should be implicit (felt
  through consequences) not explicit (more controls). Ask before adding UI inputs.
- **Purse/coffers conservation.** Every new spend or gain must balance: if purse
  decreases by X, something else increases by X (or it is explicitly a loss/fine).
  Add a `ConservationTests` check for any new flow.
- **Update the golden-run hash** in `GoldenRunTests.cs` after every task that
  changes simulation output.
- **Keep `Game.Core` Unity-free.** No `UnityEngine` imports in any file under
  `Assets/Scripts/GameCore/`. Unity-layer code lives in
  `Assets/Scripts/Unity/`.
- **Tests for every task.** Each task lists specific assertions. Add them; do not
  skip. Run `dotnet test Game.CoreTests.csproj` to verify before committing.
- **Commit after each task** with a message in the form:
  `feat(task-N): <one line description>`.
