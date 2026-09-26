# Progress log

Newest entries at the bottom. One entry per backlog task, plus milestone reports and gate reports.

Entry template:

```
## <task id> — <title> (<date>)
- Done: <what changed, 1-3 lines>
- Tests: <tests un-skipped / added>
- Decisions: <ADR ids or "none">
- Golden: <"unchanged" or "regenerated: <why>">
- Perf: <numbers if touched, else "n/a">
- Next: <anything the next task must know>
```

---

## SCAFFOLD — initial repository (2026-09-25)
- Done: docs, specs, backlog, data files, solution, Aurvangar.Sim foundation (Core, ContentDb, VoxelWorld, Simulation
  loop, commands, events, state hash), scaffolds with typed stubs for water, pathing, agents, actions, buildings,
  save. ViewCore mesher stubs. Headless runner. Godot project shell with fixed-tick GameRoot. Scripts, hooks,
  agents, CI.
- Tests: 35 foundation tests pass. 124 acceptance tests are skipped with their backlog task id.
- Verified in the scaffolding environment: Aurvangar.Sim, Aurvangar.ViewCore and Aurvangar.Headless build with .NET 8.0.131;
  the test sources compile and the non-skipped tests pass under a local xUnit stand-in. NOT verified there (NuGet
  was unreachable): the real xUnit run and the Godot.NET.Sdk build. M0-T1 verifies both.
- Next: M0-T1.

## M0-T1 — Verify toolchain and repo (2026-09-26)
- Done: Confirmed `git status` clean on `main` before starting. `./scripts/check.sh` green: sim-guard OK, solution
  builds with 0 warnings, Godot csproj (`Godot.NET.Sdk/4.6.2`, restored from NuGet) builds. No code changes.
- Tool versions: Windows 11 (Git Bash), git 2.50.1.windows.1; .NET SDKs 6.0.428, 8.0.425, 9.0.312, 10.0.201
  (default `dotnet` 10.0.201, all projects target net8.0); xunit 2.9.2, xunit.runner.visualstudio 2.8.2,
  Microsoft.NET.Test.Sdk 17.11.1. Godot: winget `GodotEngine.GodotEngine` 4.6.1.stable.official — the standard
  (non-.NET) edition, so it cannot build/run C# projects. `GODOT_BIN` is unset; the
  `--headless --build-solutions` step was skipped per CLAUDE.md.
- Tests: 35 passed, 118 skipped, 0 failed (non-Perf). Plus 6 skipped Perf tests = 124 acceptance skips, matching
  the scaffold count.
- Decisions: none
- Golden: unchanged
- Perf: n/a
- Next: M0-T2 (CI). Screenshot/editor tasks (M3-T6 onward) need a Godot 4.6 .NET (mono) binary in `GODOT_BIN`;
  the installed winget build is not the .NET edition.

## M0-T2 — CI (2026-09-26)
- Done: No code changes. `act` is not installed, so verified by inspection plus real GitHub runs:
  `.github/workflows/ci.yml` job `check` runs `./scripts/check.sh` verbatim (checkout + setup-dotnet 8.0.x), so it
  mirrors the local gate exactly (sim-guard, sln build, non-Perf tests, Godot csproj build). Job `perf` runs
  `./scripts/perf.sh` with `PERF_SCALE=2.0` and `continue-on-error: true` (non-blocking); PerfHelpers reads
  `PERF_SCALE`. Scripts are committed as mode 100755 and LF (.gitattributes), so they run on ubuntu.
  `gh run list`: last 5 `ci` runs on main all succeeded (e.g. run 36253357192: check 21s, perf 27s).
  Local `./scripts/check.sh` green; local `./scripts/perf.sh` runs (6 Perf tests, all skipped).
- Tests: 35 passed, 118 skipped, 0 failed (non-Perf); 6 Perf skipped. None un-skipped (task has no tests).
- Decisions: none
- Golden: unchanged
- Perf: n/a
- Next: M1-T1/M1-T2 are done; next is M1-T3 (terrain generator). Follow-up, not blocking: GitHub warns that
  actions/checkout@v4 and actions/setup-dotnet@v4 target deprecated Node 20, and ubuntu-latest moves to Ubuntu 26
  from 2026-10-19; bump to v5 actions / pin the runner when convenient.

## M1-T3 — Terrain generator (2026-09-26)
- Done: `TerrainGenerator` (GEN-01..08 data) split into `TerrainShape` (2-octave integer value noise 18..26, hill
  at (90,40) r26 +16 clamped 48, river table from `Fixed.Sin`, channel carve), `TerrainGenerator` (spawn flat to
  median + north shift rule, layers, sources/drains/initial water lists) and `TerrainPlants` (seeded dart throwing:
  150 trees spacing ≥ 4, 24 bushes). Hub origin is computed from the hub footprint in data
  (`TerrainResult.HubOrigin(footprint)`, used by `WorldFactory`) per sim-reviewer. Seed 1: spawn flat at (40,60),
  no shift; 150 trees + 24 bushes; 18 BuildingSolid; headless creation 15 ms.
- Tests: un-skipped `TerrainGeneratorTests.*` (5) and `WorldInvariantTests.EveryColumnHasBedrockAtZero`,
  `HubFootprintIsBuildingSolid_EntranceStandable`, `TreesRegisteredAsPlants`. Added
  `TerrainGeneratorTests.HubOnSpawnFlat_BushesNearRiverAndHub` and `Plants_CountsHoldAcrossSeeds` (seeds 2..5).
  check.sh: 48 passed, 110 skipped, 0 failed.
- Decisions: ADR-009 (river channel geometry: constant bed, lowest water y=14, fill 14..17; banks slope 1/cell
  until natural terrain; "distance from river" = cells from channel edge along Z).
- Golden: unchanged (no golden file yet; M1-T4 creates it)
- Perf: n/a
- Next: M1-T4. `WorldFactory` does not yet register sources/drains or fill water (M2-T5); `TerrainResult` exposes
  `WaterSources`, `WaterDrains`, `InitialWater`, `SpawnX/Z`, `SpawnSurfaceY`. Initial water covers every air cell
  with y ≤ 17 (channel plus lower bank cells). Reviewer note for M4-T3: flattening to the median can leave steps
  of several blocks at the flat edges; check `HubReachesRiverAndHill` when pathing lands.

## M1-T4 — State hash and golden scaffold (2026-09-26)
- Done: `StateHash()` now also covers water sources/drains (list order) and `WaterStats` (moved into
  `WaterGrid.AddToHash`), the `IdAllocator.Next` of plants, buildings and agents, and every agent path cell (was
  only the path length). Everything else that exists (clock, RNG, blocks, levels, source strength, plants,
  buildings, agents) was already hashed. Added `tests/golden/*.txt text eol=lf` to `.gitattributes`.
- Tests: un-skipped `GoldenHashTests.TwoRunsMatch` and `MatchesGoldenFile`. Added
  `SimulationTests.StateHash_CoversAllExistingState` (theory, 11 mutations) and `StateHash_CoversAgentPathCells`;
  7 of those failed before the change. check.sh: 62 passed, 108 skipped, 0 failed.
- Decisions: none
- Golden: created `tests/golden/seed1.txt` (seed 1, ticks 0/1200/3000/6000) via `UPDATE_GOLDEN=1`. Water/agents
  are still inert, so all four hashes differ only by clock; expect regeneration at M2-T5.
- Perf: n/a
- Next: M1-T5 (headless runner). Any new sim state must be added to the owning system's `AddToHash` and to the
  `StateMutations` list in `SimulationTests`.

## M1-T5 — Headless runner (2026-09-26)
- Done: `tools/Aurvangar.Headless/Program.cs` (scaffolded) now prints trees and bushes separately
  (`plants.trees`, `plants.bushes`) and a `run:` line with elapsed seconds and ticks/sec, alongside the existing
  block counts by type, buildings, agents, water volume, per-interval tick_ms and final hash.
  `./scripts/run-headless.sh --seed 1 --ticks 1000`: 150 trees, 24 bushes, world created in ~18 ms,
  ~1.5M ticks/sec (sim is still mostly inert), hash `26ece79b4c0278b8`, identical across two runs.
- Tests: none un-skipped (task has no acceptance tests). check.sh: 62 passed, 108 skipped, 0 failed.
- Decisions: none
- Golden: unchanged
- Perf: n/a
- Next: M1 complete. Next is M2-T1 (WaterGrid storage, active set, fall). `--script` still returns exit 2 until
  M5-T7/M6-T6.

## M2-T1 — WaterGrid storage, active set, fall (2026-09-26)
- Done: `WaterGrid` is now a partial class. `WaterActiveSet` (flag array + index list, sorted before each step)
  implements WAT-02; `SetLevel` activates the cell and its neighbors when the level changes. `WaterGrid.Step.cs`:
  double-buffered step (WAT-03) with fall (WAT-04) into an `int[]` delta, applied in ascending index order with
  WAT-16 overfill resolution (excess pushed up, or counted as `Evaporated` under a solid/world ceiling); changed
  cells plus their neighbors form the next active set. Spread, sources/drains, world-change consumption and
  `WaterDirty` events are not done yet (M2-T2..T4).
- Tests: un-skipped `WaterGridTests.Fall_MovesOneCellPerStep`, `Fall_TopsUpPartialCellBelow`,
  `Fall_ActiveSetEmptiesWhenSettled`, `Conservation_SingleColumn` (the three Fall tests failed against the no-op
  Tick first). Added `ActiveSet_SetLevelActivatesWetCellsOnly`, `Fall_ThroughOpenAirLandsOnFloor`.
  check.sh: 68 passed, 104 skipped, 0 failed. sim-reviewer: no required fixes.
- Decisions: ADR-010 (active set admits only wet cells; it is sim state and is hashed as sorted indices).
- Golden: regenerated with `UPDATE_GOLDEN=1 dotnet test ... --filter Category=Golden` (golden files cannot be
  hand-edited here): the hash now includes the water active set (empty on seed 1, which has no water until M2-T5).
- Perf: n/a (headless seed 1, 1000 ticks: hash `951258ed499b8b08`, still ~0 ms/tick).
- Next: M2-T2 (spread, minimum flow, evaporation) goes in `ComputeFlows` in `WaterGrid.Step.cs`. The WAT-16
  overfill path cannot run under fall alone; once spread exists, add tests for overfill under open air and
  under a solid ceiling (conservation must hold). M4-T10 must save/load the active set (ADR-010).

## M2-T2 — Spread, minimum flow, evaporation (2026-09-26)
- Done: `WaterGrid.Step.cs` `ComputeFlows` now does WAT-07 film evaporation (checked first, solid floor only),
  WAT-04 fall, WAT-05 spread `(L - level[n]) / 5` to lower open horizontal neighbors once the cell below is solid or
  full, and WAT-06 1-unit minimum flow to the single lowest neighbor (ties: lowest index). WAT-08 holds by
  construction. WAT-09 sources (raise-only) are applied at the start of `Tick` because `ShaftFillsBottomUp` needs
  them. Drains are still M2-T3.
- Tests: un-skipped `WaterGridTests.Spread_NotWhileFalling`, `Spread_EqualizesInChannel`, `Spread_NeverFlowsUp`,
  `WaterScenarioTests.SingleCellSpreadsAndEvaporates`, `ShaftFillsBottomUp` (3 failed before the change for the
  right reason; the other 2 guard against regressions). I fixed `ShaftFillsBottomUp`: its per-tick assertion
  contradicted WAT-04 (ADR-011). Added `Spread_SettledChannelLeavesActiveSet`,
  `MinimumFlow_TieGoesToLowestIndex`, `MinimumFlow_GoesToLowestNeighborOnly`, `Evaporation_IsolatedFilmOnFloor`,
  `Evaporation_NotNextToDeeperWater_NorWhileFalling`, `Overfill_PushesExcessIntoOpenCellAbove` and
  `Overfill_UnderSolidCeilingEvaporatesExcess` (the WAT-16 tests that M2-T1 asked for).
  check.sh: 80 passed, 99 skipped, 0 failed. The Sim diff is about 95 lines, so no sim-reviewer run was needed.
- Decisions: ADR-011 (snapshot semantics for WAT-06/07, raise-only sources, shaft test "bottom up").
- Golden: unchanged (seed 1 has no water until M2-T5, and the golden test still passes).
- Perf: n/a
- Next: M2-T3 needs drains (WAT-10: zero at end of step, count `Drained`). `ApplySources` already exists. Note
  that a source's stream arrives as alternating Full slugs under double buffering, which slows fill rates; check
  `BasinFillsThenOverflows` (2,000 ticks) against this.

## M2-T3 — Sources, drains, stats, conservation (2026-09-26)
- Done: `WaterGrid.Step.cs` adds `ApplyDrains` (WAT-10). It runs after the deltas are applied, zeroes each drain
  cell, counts the volume in `Stats.Drained`, and marks the drain changed so its wet neighbors activate and keep
  flowing in. Sources (WAT-09, raise-only) were already in place from M2-T2. WAT-11 conservation now holds with
  sources and drains.
- Tests: un-skipped `WaterGridTests.Conservation_RandomPours`, `Source_AddsVolume_Drain_RemovesIt`,
  `Source_StrengthZero_AddsNothing`, and `WaterScenarioTests.BasinFillsThenOverflows`. Before the change, the drain
  test failed on `Drained > 0` (right reason). The basin test failed on "never overflowed" because of its geometry
  (ADR-012): I moved its source from (1,7,1) to (1,8,1). Added `Conservation_SourceAndDrain_EveryTick` (strength 50,
  two drains, conservation checked every tick). check.sh: 85 passed, 95 skipped, 0 failed.
- Decisions: ADR-012 (the basin scenario source sits one cell above the rim; WAT-09 keeps set/raise semantics).
- Golden: unchanged (seed 1 has no sources or drains registered until M2-T5).
- Perf: n/a
- Next: M2-T4 (world interaction). `WaterGrid` does not yet consume `World.CellChanged`, and it emits no `WaterDirty`
  events (the `events` parameter of `Tick` is still unused). Drain cells are only zeroed at the end of the step, so
  they hold water while that step is computed, and neighbors see a drain as an empty cell only from the next step.

## M2-T4 — World interaction (2026-09-26)
- Done: New `WaterGrid.World.cs`. `WaterGrid` reads `World.ChangedCells` through a cursor, at the start of `Tick`
  (commands, setup) and in the new `Water.EndTick(Events)`, which `Simulation.Tick` calls after `Agents.Tick` and
  before `PathGrid.Invalidate`/`ClearChangeLog`. Each changed cell and its neighbors are activated (WAT-13). A cell that
  is now solid pushes its water out (WAT-12: equal capped shares to the open horizontal neighbors, the rest goes up,
  and any overflow is counted as `Evaporated`). WaterDirty (WAT-15) fires per chunk on a drift of 32 or more from the
  per-cell baseline, or on a 0 ↔ wet crossing. ARCH-01 in `docs/01-architecture.md` documents the EndTick step.
- Tests: un-skipped `WaterScenarioTests.BreachFloodsTunnel`, `LeveePushConservesVolume`,
  `WaterDirty_EmittedForChangedChunks` (all 3 failed first for the right reasons). Added `WaterWorldTests` (6 tests:
  push split/remainder, neighbor cap, enclosed evaporation, dig activation, throttle at 31/32, wet/dry crossing).
  check.sh: 94 passed, 92 skipped, 0 failed. The Sim diff is about 150 lines. The sim-reviewer's one required fix (the
  ARCH-01 doc step) is applied, plus these suggestions: reset the cursor if the log was cleared, and a
  "WaterDirty is view-only" note.
- Decisions: ADR-013 (the change-log cursor with two consume points, push capping, WaterDirty baselines not hashed or
  saved).
- Golden: unchanged (seed 1 has no water; headless 1000-tick hash is still `951258ed499b8b08`).
- Perf: n/a (the water perf tests are still skipped until M2-T6).
- Next: M2-T5 pre-settle in `WorldFactory` must run through `Simulation.Tick` (or call `Water.EndTick` after each
  `Water.Tick`) so the change cursor and WaterDirty stay consistent. Reviewer notes for later: `ChunkOfCell` uses
  `CellOf` + `ChunkIndexOf` per changed cell (check it in M2-T6 perf). A chunk can get WaterDirty twice in one tick, so
  the view queue must dedupe (M3-T4). Border cells do not dirty neighbor chunks (M3-T3).

## M2-T5 — River on seed 1 (2026-09-26)
- Done: `WorldFactory.PreSettleRiver` registers GEN-08 sources (x=0, y 14..17) and drains (x=127, y 14..top), fills
  `InitialWater` to `Full`, runs 600 full `Simulation.Tick`s (keeps the ADR-013 change cursor consistent), then resets
  the clock to 0, zeroes `WaterStats` (new `WaterGrid.ResetStats`) and drains the event bus. Headless seed 1: world
  creation ~117 ms (was ~18 ms), 1200 ticks: water.active=1602, volume 4,785,289 → 4,449,058, median tick 0.17 ms;
  6000 ticks: active 2319, volume 3,715,828 (the flat-bed river keeps losing head to the drains; WAT-P2 is only
  specified at 1200 ticks).
- Tests: un-skipped `RiverTests.Seed1_RiverFlowsAcrossMap` (failed first: no water at x=64) and `Seed1_RiverSettles`
  (passed vacuously before, 1602 ≤ 3000 now). Added `Seed1_PreSettleResetsStatsAndEvents_ConservationFromTickZero`
  and `Seed1_CreateIsDeterministic`. check.sh: 98 passed, 90 skipped, 0 failed. Sim diff ~30 lines (no sim-reviewer).
- Decisions: ADR-014 (pre-settle through the full tick loop; clock, water stats and events reset after it).
- Golden: regenerated with `UPDATE_GOLDEN=1 dotnet test tests/Aurvangar.Sim.Tests --filter Category=Golden` (golden
  files cannot be hand-edited here): seed 1 now has a settled, flowing river. Headless 1200-tick hash `7757381ef0513c61`.
- Perf: WAT-P2 1602/3000 active cells at tick 1200. Water perf tests still skipped until M2-T6.
- Next: M2-T6 (water perf). Every `WorldFactory.Create` now costs ~100 ms of pre-settle, so tests creating seed 1
  worlds are slower. Anything that acts in `Tick` and is added to `WorldFactory` (colonists M4-T4, starting stock
  M5-T5) must be added after `PreSettleRiver` (ADR-014). Active count creeps up after 1200 ticks (2319 at 6000);
  watch it in M6-T7 full-tick perf.
