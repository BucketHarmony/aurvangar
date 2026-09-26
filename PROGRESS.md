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
