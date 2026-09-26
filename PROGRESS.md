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

## M2-T6 — Water perf (2026-09-26)
- Done: New `Core/IndexSort` (LSD radix sort for cell indices) replaces `List.Sort` in `WaterActiveSet.Sorted` and
  `WaterGrid.ApplyDeltas`. Profiling showed those two sorts took about 1 ms of a ~3 ms step. The hot water methods are
  marked `AggressiveOptimization`, so short perf runs do not measure tier-0 JIT code. Water results are
  identical (same sort order).
- Tests: un-skipped `WaterPerfTests.Step_20kActiveCells_Under4ms`, which passed before the change at ~2.5 ms median
  but only has ~16k active cells. Added `Step_Sustained20kActiveCells_Under4ms` (192x128 layer, asserts at least 20,000
  active cells on every measured step; before the fix a 160x128 layer ran 3.1-4.0 ms median, within 20% of budget).
  Added `Seed1_SettledRiver_ActiveCellsAndStep` (WAT-P2 in the perf run) and `CoreTests.IndexSort_MatchesListSort`
  (5 cases). check.sh: 103 passed, 90 skipped, 0 failed. perf.sh: 3 passed, 5 skipped. The Sim diff is ~60 lines, so
  I did not run the sim-reviewer.
- Decisions: ADR-015 (radix index sort, AggressiveOptimization, sustained-20k test sizing).
- Golden: unchanged (the golden test passes; the sort output is identical).
- Perf (Release, this machine): WAT-P1 128x128 median 1.24-1.28 ms (p95 1.8-2.3); WAT-P1 192x128 (23k active)
  median 1.89-1.92 ms (p95 3.0-3.8) against a 4 ms budget; WAT-P2 1602/3000 active cells at tick 1200, with a settled
  river step median ~0.09 ms.
- Next: M2 is complete except the milestone check. Next unchecked task is M3-T1 (greedy chunk mesher, ViewCore).
  Reuse `IndexSort` for other hot index sorts. p95 on the 23k load reaches ~3.8 ms (the budget is on the median).

## M3-T1 — Greedy chunk mesher (2026-09-26)
- Done: `ViewCore.Meshing.ChunkMesher.Build` implemented (VIEW-03). It copies the chunk plus a one-cell border into a
  34³ padded grid of visible block ids (WLD-04 out-of-bounds: Bedrock below, Air elsewhere; cells above `sliceY` count
  as Air), then for each of the 6 face directions and 32 layers builds a 32x32 face mask (solid cell next to a
  non-solid cell) and greedily merges runs of the same block id. Positions are in world coordinates. Quads are
  wound counter-clockwise from the normal side (the `MeshData` contract). Colors come from `BlockColors`
  (palette.json). ViewCore only; no Sim changes.
- Tests: un-skipped all 6 `ChunkMesherTests` (all failed first with the M3-T1 `NotImplementedException`). Added
  `QuadsWoundCounterClockwiseFromNormalSide`, `FacesCulledAcrossVerticalChunkBorder_AndColorsFromPalette`,
  `EmptyChunk_IsEmpty`. check.sh: 112 passed, 84 skipped, 0 failed.
- Decisions: none
- Golden: unchanged
- Perf: n/a (MESH-P1 is M3-T7)
- Next: M3-T2 only has to add the cut flag. The mask value is the block id and has a `// M3-T2` hook where the
  cut bit (for example `| 1 << 8`) goes, so cut and uncut faces never merge. Colors use `m & 0xFF`, so use
  `colors.GetCut` when the bit is set and pass `isCut` to `AddQuad`. Slicing-as-Air is already in `FillPadded`.
  M3-T4: Godot's default front face is clockwise, so the Godot layer must reverse the index order (or set cull
  mode) when it copies `MeshData` into an `ArrayMesh`. Positions are in world space, so place chunk MeshInstances at
  the origin.

## M3-T2 — Slicing in mesher (2026-09-26)
- Done: `ChunkMesher` flags slice cut faces (VIEW-04): a top face of a solid cell at `y == sliceY` whose real-world
  cell above is solid gets a cut bit (`1 << 8`) in the greedy mask key, is colored with `BlockColors.GetCut`, and is
  passed to `AddQuad` with `isCut` (counted in `CutQuadCount`). Cells above the slice were already Air (M3-T1).
  ViewCore only; no Sim changes.
- Tests: un-skipped `SliceTests.NoSlice_ColumnHasNoCutFaces`, `Slice_CutsColumnWithOneCutFace` (failed first:
  CutQuadCount 0), `BlocksAboveSlice_Hidden` (already passed from M3-T1). Added `Slice_AtNaturalSurface_NoCutFace`,
  `CutAndUncutTops_DoNotMerge_AndCutIsDarkened`, `Slice_AtChunkTop_CutUsesCellInChunkAbove`. check.sh: 118 passed,
  81 skipped, 0 failed.
- Decisions: ADR-016 (cut only where the real cell above is solid; cut bit in the mask key).
- Golden: unchanged
- Perf: n/a (MESH-P1 is M3-T7; the cut check adds one `world.IsSolid` call per visible top face on the slice layer)
- Next: M3-T3 (WaterMesher, ViewCore). It should also hide water above `sliceY` (VIEW-04). M3-T5 slice controller:
  remesh the chunks containing the old and new slice (ADR-016).

## M3-T3 — Water surface builder (2026-09-26)
- Done: `ViewCore.Meshing.WaterMesher.Build` implemented (water.md rendering contract, VIEW-07). A surface cell (wet,
  above dry/solid/OOB/above slice) gets one top quad at `y + level/Full`. Every wet cell gets side quads toward
  non-solid horizontal neighbors whose water top is lower (dry = `y`), so columns and falls have no gaps. Cells above
  `sliceY` are ignored (VIEW-04). Neighbors in other chunks come from the world. New `WaterColors` (palette
  `water.shallow/deep/alpha`) tints by column depth (full dark at 3 cells); `Build` takes an optional `WaterColors`
  (default: embedded palette). ViewCore only; no Sim changes.
- Tests: moved `WaterMesherTests` into `Meshing/WaterMesherTests.cs` and un-skipped its 3 tests (all failed first
  with the M3-T3 `NotImplementedException`). Added `QuadsWoundCounterClockwiseFromNormalSide`,
  `SideQuad_SpansLevelDifferenceToLowerNeighbor`, `FreeStandingColumn_SidesCoverWholeHeight`,
  `Slice_HidesWaterAbove_AndCellAtSliceBecomesSurface`, `NeighborInOtherChunk_IsReadFromWorld`,
  `Color_DarkerWithDepth_AndSemiTransparent`. check.sh: 127 passed, 78 skipped, 0 failed.
- Decisions: ADR-017 (side faces for every wet cell, column-depth tint, slice handling, optional colors).
- Golden: unchanged
- Perf: n/a (no water mesh budget; one top quad per surface cell, no greedy merge, per the contract)
- Next: M3-T4 (Godot GameRoot loop, ChunkRenderer, WaterRenderer). The water mesh of a chunk reads its one-cell
  border, so when water changes in a border cell the WaterRenderer must also remesh the neighbor chunk (WaterDirty
  is only raised for the chunk of the changed cell), and dedupe WaterDirty per frame. Water material needs alpha
  blending (vertex color alpha = `water.alpha`) and, like terrain, reversed index order or a cull-mode change for
  Godot's clockwise front faces. Godot cannot be run on this machine (standard 4.6.1, no GODOT_BIN), so M3-T4
  rendering can only be build-verified.

## M3-T4 — Godot: GameRoot loop, ChunkRenderer, WaterRenderer (2026-09-26)
- Done: New ViewCore `Frame/TickAccumulator` (VIEW-01 fixed step, speeds 0/1/3/6, max 4 ticks/frame, backlog
  clamp), `Frame/RemeshQueue` (deduping FIFO), `Frame/RemeshRouter` (ChunkDirty → terrain + water; WaterDirty → water
  of the chunk and its 6 face neighbors; budgets 4 + 4, VIEW-02) and `Meshing/MeshWinding` (CCW → Godot CW indices,
  collision triangle soup). Godot: `GameRoot` uses them and creates `ChunkRenderer` (per-chunk MeshInstance3D with
  vertex-color material + StaticBody3D/ConcavePolygonShape3D on layer 1 for M3-T5 picking) and `WaterRenderer`
  (alpha-blended, cull disabled, no shadows); `MeshConvert` copies `MeshData` into `ArrayMesh`. All chunks are
  queued on load. Main.tscn unchanged (renderers are created in code). No Sim changes.
- Tests: added `View/FrameLoopTests.cs` (11 tests: accumulator, queue, router, winding, and
  `SeedOneFrameLoopTests.InitialLoad_MeshesTerrainAndRiver_WithinBudgetedFrames`, which drives the seed-1 world through
  the queues and meshers headless and asserts terrain and river quads). All failed first with the M3-T4
  `NotImplementedException` stubs (budget constant test aside). check.sh: 138 passed, 78 skipped, 0 failed; Godot
  csproj builds with 0 warnings.
- Decisions: ADR-018 (view loop logic in ViewCore; WaterDirty neighbor remesh; collision built now).
- Golden: unchanged
- Perf: n/a (no budget for this task; MESH-P1 is M3-T7)
- Godot run NOT verified: only the standard (non-.NET) Godot 4.6.1 is installed and GODOT_BIN is unset, so the scene
  was not opened or played; "seed 1 renders with terrain and river" is verified only via the headless ViewCore test
  and a successful `dotnet build` of the Godot project. First real run should check winding (faces not inside-out)
  and water transparency sorting.
- Next: M3-T5 (camera rig, slice controller, picking, F3). `GameRoot.SliceY` exists (private set, default SizeY-1);
  on a slice change, queue terrain + water for chunks whose Y range contains the old or new slice (ADR-016) via
  `Remesh.Terrain/Water.Enqueue`. Picking: ray against `ChunkRenderer.PickLayer`; the StaticBody3D has meta
  "chunk". The scene's Camera node is still a fixed transform.

## M3-T5 — Godot: camera rig, slice controller, picking, debug overlay (F3) (2026-09-26)
- Done: New ViewCore `Camera/OrbitRig` (VIEW-06: pan relative to yaw, Q/E 90 degree steps tweened over 0.2 s, zoom
  10-120, drag pitch 25-80, focus height follows the slice), `Frame/SliceController` + `RemeshRouter.EnqueueSliceChange`
  (VIEW-04: clamp 0..SizeY-1, remesh terrain + water of the old and new slice chunk layers only),
  `Picking/PickResolver` (VIEW-05: hit point + normal -> cell and face, ignored above the slice / out of world),
  `Diagnostics/{RollingAverage, RateMeter, PhaseTimer, DebugOverlayText}` (VIEW-17). Sim: optional
  `ITickProfiler` hook (`Simulation.Profiler`) bracketing the water step and region rebuild; not state, hash unchanged.
  Godot: `CameraRig` (Camera3D script, attached in Main.tscn), `DebugOverlay` (F3, CanvasLayer + Label),
  `HoverMarker` (box on the picked cell), GameRoot handles PageUp/PageDown/[/] and F3, raycasts layer 1 in
  `_PhysicsProcess`, times `Sim.Tick()`.
- Tests: added `View/CameraSliceTests.cs` (6 OrbitRig, 5 slice/pick) and `View/DebugOverlayTests.cs` (6: rolling
  average, rate, phase timer on a real tick, profiler does not change the hash, overlay text fields). All failed first
  to compile (types missing). check.sh: 155 passed, 78 skipped, 0 failed; Godot csproj builds with 0 warnings.
- Decisions: ADR-019 (focus height rule, profiler hook, pick/slice/rotate details).
- Golden: unchanged
- Perf: n/a (no budget for this task; the profiler hook is two null checks per tick when unset)
- Godot run NOT verified: only the standard (non-.NET) Godot 4.6.1 is installed and GODOT_BIN is unset. Camera
  controls, picking, F3 and the edited Main.tscn (CameraRig script on the Camera node) are build-verified only.
- Next: M3-T6 (screenshot harness) can position the camera with `OrbitRig` (focus/yaw/pitch/distance, `SetYaw`) and
  set the slice with `SliceController.Set`. It needs a Godot .NET binary to actually render; without GODOT_BIN it can
  only be build-verified. Open jobs in F3 are wired as `OpenJobsByKind = null` in `GameRoot.Snapshot()` (M4-T6).

## M3-T6 — Screenshot harness (2026-09-26)
- Done: ViewCore `Screenshots/ScreenshotArgs` (VIEW-20 user args: --seed/--ticks/--shots/--out, defaults, validation),
  `Screenshots/ScreenshotPresets` (overview/river/hub/slice computed from the world, `CameraShot.ApplyTo(OrbitRig)`),
  `OrbitRig.SetView`, `Meshing/PlantMesher` + `PlantColors` (tree = trunk box + canopy cone, bush = small cone;
  slice-aware). Godot: `ScreenshotRunner.cs` + `scenes/Screenshot.tscn` (Node root instancing Main.tscn as "Main";
  pauses real time, runs ticks at once, per shot sets slice, flushes all remeshes, waits 3 drawn frames, saves
  `<out>/<preset>.png`, quits 0/1/2), `PlantRenderer` (plants now render in the game too), GameRoot `RunTicksNow`,
  `FlushRemesh`, `ApplyShot`, `PickingEnabled`; `CameraRig.ApplyNow`. `scripts/screenshot.sh` rewritten: clear error
  when GODOT_BIN is unset / not executable / not a 4.6 mono build, `pwd -W` paths on Git Bash, xvfb-run only on
  Linux without DISPLAY, fails if any requested PNG is missing. No Sim changes.
- Tests: added `View/ScreenshotTests.cs` (22: args parsing 8, presets 6, plant mesher 5, Godot scene file references
  3). All failed first to compile (types missing). check.sh: 177 passed, 78 skipped, 0 failed; Godot csproj builds
  with 0 warnings.
- Decisions: ADR-020 (preset numbers, harness scene structure, plant placeholder shapes, script checks).
- Golden: unchanged
- Perf: n/a (plant mesh is one mesh rebuilt only on slice / plant-count change)
- Screenshots NOT produced: GODOT_BIN is unset and the only Godot here is the standard 4.6.1 (non-.NET) edition.
  Verified `./scripts/screenshot.sh` exits 1 with a clear message for: GODOT_BIN unset, GODOT_BIN=/nope, and
  GODOT_BIN = the winget standard 4.6.1 console exe ("the standard edition. The C# project needs the .NET (mono)
  edition"). Screenshot.tscn / the runner are build-verified and statically checked only.
- Next: M3-T7 (mesher perf, MESH-P1). The M3-GATE report needs the 4 PNGs; on this machine it can only list that
  they could not be produced (install Godot 4.6 .NET and set GODOT_BIN, then run `./scripts/screenshot.sh`).

## M3-T7 — Mesher perf (2026-09-26)
- Done: Moved the MESH-P1 placeholder out of `Perf/PerfTests.cs` into `Perf/MesherPerfTests.cs` with a real body:
  mesh every seed-1 chunk at full slice, pick the one with the most quads ("busiest surface chunk"), time
  `ChunkMesher.Build` on it (5 warmup, 50 iterations, median <= 6 ms * PERF_SCALE), and also assert the same budget
  with the slice cutting through the middle of that chunk. No production code changes: the M3-T1/T2 mesher already
  meets the budget, so the un-skipped test passed on first run (the placeholder it replaces throws by design).
- Tests: `Perf/MesherPerfTests.Mesher_SurfaceChunk` (replaces the skipped placeholder). check.sh: 177 passed,
  78 skipped, 0 failed (perf tests are not part of check.sh); Godot csproj builds with 0 warnings. perf.sh: 4 passed,
  4 skipped (M4-T12 x2, M6-T1, M6-T7).
- Decisions: none
- Golden: unchanged
- Perf (Release): MESH-P1 chunk (3,0,2), 581 quads: median 0.76 ms, p95 1.25 ms; sliced at y=16 (265 quads, 44 cut):
  median 0.57 ms (budget 6 ms). WAT-P1 128x128 median 1.18 ms / p95 2.55 ms; 192x128 (>= 23k active) median 2.00 ms /
  p95 3.95 ms (budget 4 ms on the median; the median has 50% headroom). WAT-P2 1602 active cells, step 0.086 ms.
- Next: M3-GATE (HUMAN-GATE G1). Screenshots cannot be produced here (GODOT_BIN unset; only standard Godot 4.6.1
  installed), so the gate report must say so and give the perf numbers above.

## M3-GATE — HUMAN-GATE G1: world and water look (2026-09-26)

**Status: waiting for human review. The gate box in BACKLOG.md is NOT checked; check it after review.**
All M3 tasks (T1..T7) are checked. No M4 work has started.

### Build and test results (this run)
- `./scripts/check.sh`: OK. 177 passed, 78 skipped (acceptance tests for later milestones), 0 failed. The Godot
  csproj builds with 0 warnings.
- `./scripts/perf.sh`: OK. 4 passed, 4 skipped (M4-T12 x2, M6-T1, M6-T7).

| Budget | Measured (Release, this machine) | Limit |
|---|---|---|
| WAT-P1 128x128 (~15.5k active) | median 1.16 ms, p95 1.74 ms | 4 ms median |
| WAT-P1 192x128 (>= 23,048 active every step) | median 2.06 ms, p95 3.24 ms | 4 ms median |
| WAT-P2 seed-1 river after 1200 ticks | 1602 active cells, step median 0.086 ms | 3000 cells |
| MESH-P1 busiest seed-1 chunk (3,0,2), 581 quads | median 0.81 ms, p95 2.06 ms | 6 ms |
| MESH-P1 same chunk sliced at y=16 (265 quads, 44 cut) | median 0.56 ms | 6 ms |

- Headless smoke run `run-headless.sh --seed 1 --ticks 24000 --report-every 2400`: world created in 58 ms; 150
  trees, 24 bushes, 1 building (the hub). Tick median 0.085-0.096 ms, p95 <= 0.194 ms, 9,735 ticks/s. Active water
  cells 1,901-2,462. Final hash `1466e1f6d7ad677c`. No agents yet (they arrive in M4).
- Water volume on seed 1 falls from 4,785,289 at tick 0 to 2,747,763 at day 10 (-43%, about 1,990 full cells)
  with no drought. It is still falling slowly (-50k over the last 2400 ticks). The conservation tests pass, so the
  water leaves through drains and evaporation and is not lost to a bug. The likely cause is the initial fill of the
  lower bank cells (ADR-009) draining away, plus evaporation of the thin film on the banks. The active cell count
  stays within the WAT-P2 budget. See Q4.

### Screenshots: NOT produced
The only Godot on this machine is the standard (non-.NET) 4.6.1 from winget, and `GODOT_BIN` is unset. So
`./scripts/screenshot.sh` stops with a clear error and `artifacts/screens/` does not exist.

**No Godot-side code from M3-T4..T6 has ever been run.** GameRoot, ChunkRenderer, WaterRenderer, PlantRenderer,
CameraRig, DebugOverlay, HoverMarker, ScreenshotRunner, Main.tscn and Screenshot.tscn have only been compiled and
checked with static tests. The ViewCore code they call (meshers, queues, camera math, picking, presets) is covered
by headless unit tests.

How to produce the four PNGs:
1. Install Godot **4.6 .NET** (the "mono" build, 4.6.x, to match `Godot.NET.Sdk/4.6.2`). The .NET 8 SDK is already
   installed.
2. In Git Bash: `export GODOT_BIN='C:/path/to/Godot_v4.6.x-stable_mono_win64_console.exe'`. Use the `_console.exe` so
   you can see Godot's output.
3. Recommended once: open `src/Aurvangar.Godot/project.godot` in that editor so it imports the project and creates
   `.godot/`.
4. Run `./scripts/screenshot.sh`. It writes `artifacts/screens/{overview,river,hub,slice}.png` (seed 1, 1200 ticks).
   Override with `SEED`, `TICKS`, `SHOTS` or `OUT`.

What to look at in each shot (camera numbers are in ADR-020):
- `overview.png` (world center, yaw 45, pitch 45, distance 120): terrain colors (grass, dirt, stone, sand), the hill,
  the river across the whole map, trees drawn as trunk + cone. There should be no holes, no inside-out faces and no
  seams at the 32-cell chunk borders.
- `river.png` (between the hub and the river, looking along -X): water transparency (alpha 0.72) and the
  shallow-to-deep tint, the sand banks, water side faces where the bank steps down. Look for flicker or wrong draw
  order where water overlaps terrain.
- `hub.png` (the hub, pitch 50, distance 32): the hub block (BuildingSolid) on the spawn flat, bushes nearby, and how
  big one cell looks at this zoom.
- `slice.png` (slice at y=20 over the hill peak): cut faces darkened (x0.55) only where solid rock continues above,
  nothing drawn above y=20, trees above the slice hidden.

First-run checklist for the game itself (play Main.tscn in the editor):
- **Face winding**: terrain is not inside-out (you see the outer faces, not the far walls). The CCW-to-CW flip is in
  `MeshWinding`. If everything is inverted, that flip or the material cull mode is wrong.
- **Water transparency sorting**: water is alpha-blended with culling disabled and no shadows, one mesh per chunk.
  Watch for water chunks vanishing or popping as the camera turns, and for terrain showing through where it should
  not.
- **Camera** (VIEW-06): WASD or the arrow keys pan relative to the view. Q/E rotate 90 degrees (0.2 s tween). The
  wheel zooms between 10 and 120. Middle-drag changes yaw and pitch (pitch 25-80). Pan speed equals the zoom distance
  in cells/s, so judge whether it feels too fast.
- **Slice keys** (VIEW-04): PageUp/PageDown and ]/[ move the slice one level and repeat while held. Only the affected
  chunk layers should remesh. The camera focus drops onto the slice when you slice below the colony.
- **F3 overlay** (VIEW-17): FPS, ticks/s, water ms, region ms, active water cells and remesh queue lengths. Jobs show
  "none" until M4.
- **Picking** (VIEW-05): the hover box marks the cell under the mouse, on the correct face. Nothing above the slice
  can be picked.
- **Speed**: Space pauses and resumes; 1/2/3 set x1/x3/x6. At x1 the river should look calm and settled, with no
  visible stepping.

### Known and likely visual issues
- Water top quads are one per cell and not merged (as the contract says). This is fine for looks but costs more
  triangles.
- A WaterDirty event can remesh up to 7 chunks (ADR-018), so a busy river can lag a few frames behind the sim.
- Plants are placeholders: a trunk box with a square cone canopy for trees, a small cone for bushes.
- Lighting and post-processing are untuned. Colors come straight from `data/palette.json` as vertex colors.

### ADRs from M1–M3 to sanity-check (docs/decisions.md)
- **ADR-009** River: flat bed at y=14, 7-cell channel, banks slope 1:1. The initial water also fills the low bank
  cells.
- **ADR-010** The water active set holds wet cells only and is part of the hashed and saved state.
- **ADR-011** All water rules read the pre-step snapshot. Sources only raise water, never lower it. The shaft test
  was rewritten.
- **ADR-012** The basin scenario's source moved above the rim, because a source cannot overfill its own container.
- **ADR-013** Water reads world changes twice per tick. Defines the push-out rules. WaterDirty baselines are not
  state.
- **ADR-014** The 600-tick pre-settle runs the full tick loop, then resets the clock, the water stats and the event
  queue.
- **ADR-015** The water step uses a radix sort and AggressiveOptimization; the sustained-20k perf test is heavier
  than the spec asks.
- **ADR-016** Slice cut faces appear only where the real cell above is solid, so open ground at the slice keeps its
  normal color.
- **ADR-017** Every wet cell gets water side faces; the tint follows column depth (fully dark at 3 cells deep).
- **ADR-018** View loop logic (tick clock, remesh queues, router, winding) lives in ViewCore. Collision is built per
  chunk.
- **ADR-019** Camera start (over the hub, yaw 45, pitch 45, distance 60), the focus height rule, the sim profiler
  hook, and the picking rules.
- **ADR-020** Screenshot presets are computed from the world. Placeholder plant shapes. The harness scene instances
  Main.tscn.

### Questions for the human
1. **Water look**: the colors run from shallow `#5aa9d6` to deep `#1f4f7a`, with alpha 0.72, fully dark at 3 cells
   deep. Keep these, or make the water more opaque, greener or darker? Should the surface be merged or animated for
   the POC?
2. **Slice cut color**: cut faces are the block color x0.55. Is that readable, or do you want a distinct tint or
   hatch (for example a fixed dark red-brown) so cut rock stands out?
3. **Camera feel**: pan speed equals the zoom distance in cells/s, zoom is 10-120, pitch 25-80, and the camera starts
   at distance 60 over the hub. Is it too fast or too slow? Should Q/E rotate freely instead of in 90-degree steps?
4. **River volume**: seed 1 loses about 43% of its water over 10 days, mostly the initial bank fill draining away
   (ADR-009), so by day 10 the river is visibly narrower than at tick 0. Is the day-10 width acceptable, or should the
   initial fill be limited to the channel, or the sources made stronger?
5. **Godot .NET on the build machine**: can you install Godot 4.6 .NET and set `GODOT_BIN`, so the loop can render
   screenshots itself from M4 on? Until then every Godot task is only compile-verified.

Next after approval: M4-T1 (PathGrid flag cache).

### G1 answers (human, 2026-09-26)
Screenshots were rendered after the report with Godot 4.6.2 .NET (`artifacts/screens/{overview,river,hub,slice}.png`).
The Godot side (M3-T4..T6) ran on the first try. Faces turned away from the sun render pure black (no ambient light).
1. **Water colors / 2. cut-face color**: keep the current values until the human has seen the screenshots. No water
   animation in the POC.
3. **Camera**: keep 90° Q/E steps and the current zoom and pitch limits. Pan speed is tuned after hands-on play.
4. **River volume**: keep the bank fill; fix it with stronger sources → **M3-T8**.
5. **Godot .NET**: yes. `GODOT_BIN` points at the Godot 4.6.2 .NET console binary; later gates get real screenshots.
- Added **M3-T9** (ambient lighting) for the black shaded faces.
- Gate checked. Next: M3-T8, then M3-T9, then M4-T1.

### G1 follow-up (human, after hands-on play, 2026-09-26)
- **Q1 water colors**: keep as is (`#5aa9d6` → `#1f4f7a`, alpha 0.72). Closed.
- **Q3 pan speed**: good as is. Closed.
- **Lighting**: shadows are a little harsh, so raise the ambient light some. Folded into M3-T9.
- **Q2 slice cut color**: keep the current value (block color x0.55) for now. Closed.

## M3-T8 — River holds its volume (2026-09-26)
- Done: Root cause of the G1 Q4 loss: flat bed + level-difference-only flow + inlet pinned at `Full` (x=0) + drains
  pinned at 0 (x=127) settle to a linear slope, about half the bank-filled volume, regardless of source strength
  (a source cell caps at `Full`). Fix per the human's "more source cells": spring columns every 16 cells along the
  channel (x = 0, 16, ..., 112; channel cells only, y = bed..bed+3, obeying `SourceStrength`).
  `TerrainShape.RiverSpringSpacing`, `TerrainGenerator.AddWater`. Bank fill and drains unchanged. GEN-08 text updated.
- Tests: added `RiverTests.Seed1_RiverHoldsVolumeThroughDay10` (samples every 100 ticks to day 10, ±10% of tick 0);
  it failed first at tick 1900 (89%). `TerrainGeneratorTests.River_CrossesMap_WithSourcesAndDrains` changed from
  "all sources at x=0" to the ADR-021 spring layout (the old assertion encoded the single-inlet GEN-08 rule).
  check.sh: 178 passed, 78 skipped, 0 failed; Godot csproj 0 warnings. perf.sh: 4 passed, 4 skipped.
- Decisions: ADR-021
- Golden: regenerated (`UPDATE_GOLDEN=1`): sources changed, so the pre-settled world and every hash changed.
- Perf: seed-1 water volume 4,914,791 at tick 0 → 4,905,846 at day 10 (-0.2%, was -43%). Active water cells
  670-724 over 10 days (was 1,900-2,460; WAT-P2 budget 3,000). Headless 24,000 ticks: tick median 0.034-0.054 ms,
  20,330 ticks/s, final hash `0f27c8ee613dd61c`.
- Screenshots: rendered overview/river with Godot 4.6.2 .NET; the river is full width along the whole course and
  narrows only in the last ~15 cells before the drains. Shaded faces are still black (M3-T9).
- Next: M3-T9 (ambient lighting). M6-T4 drought: springs follow `SourceStrength`, so strength 0 stops all inflow.

## M3-T9 — Ambient lighting (2026-09-26)
- Done: `Main.tscn` gets a `WorldEnvironment` (flat-color ambient `(0.75, 0.78, 0.85)` x 0.3, reflected light
  disabled, default clear-color background). Sun, water colors, palette and materials unchanged.
- Tests: added `View/SceneLightingTests` (WorldEnvironment with color ambient and energy 0.2-1.0; sun transform
  unchanged, no sun energy override); failed first on the missing WorldEnvironment. check.sh: 180 passed, 78
  skipped, 0 failed; Godot csproj 0 warnings. perf.sh: 4 passed, 4 skipped.
- Decisions: ADR-022
- Golden: unchanged (view only).
- Perf: n/a.
- Screenshots (Godot 4.6.2 .NET, seed 1, 1200 ticks): before in `artifacts/screens/before/`, after in
  `artifacts/screens/` (Compatibility/opengl3, as screenshot.sh renders) and `artifacts/screens/forward_plus/`
  (Forward+, as the game plays). Before: tree shadows, shaded tree faces and the far faces of the hill steps were
  pure black (0,0,0). After (Forward+ pixel samples, river shot): shadowed grass 68,87,63 vs lit 156,188,133
  (lit was 143,171,119), so shaded faces are about half as bright as lit ones, never black, and the grass and cones
  keep their shape in shadow. Water mid-channel 132,152,161 -> 145,168,181 and deep 104,143,163 -> 120,163,185:
  same shallow-to-deep tint, same transparency, slightly lighter; tree shadows on the water are now blue-grey
  instead of black. Slice cut faces are unchanged in relative darkness.
- Found for the human (not changed): (1) screenshot.sh uses the Compatibility renderer, which lights in gamma space,
  so its shots look brighter and more saturated than play (lit grass 122,187,84 there). (2) In Forward+ the vertex
  colors show lighter and greyer than `data/palette.json` (grass `#5f8f3e` shows as ~143,171,119 even without
  ambient) because the materials do not set `VertexColorIsSrgb`. Fixing (2) would make play match the palette but
  darken everything; worth a human decision at the next gate.
- Next: M4-T1 (PathGrid flag cache).

## M4-T1 — PathGrid flag cache (2026-09-26)
- Done: `PathGrid` caches one flag byte per cell (Valid, Standable, Walkable, Wet), computed lazily; a change
  clears the cell's 3x3x3 neighborhood. Feeds: world change log via a cursor + new `VoxelWorld.ChangeLogBase`
  (synced on every query and at tick end), `WaterGrid.WalkClassChanged` (dry/wet and shallow/deep crossings at
  every level write, including `SetLevel`), `PlantSystem.OccupancyChanged`. New `IsWet`, `InvalidateAll`,
  `SyncWorldChanges` (replaces the no-op `Invalidate`). `ScenarioBuilder.Build` calls `InvalidateAll` (raw writes).
- Tests: added `PathGridCacheTests` (compute-once counter, cached cell after dig incl. after the log is cleared,
  after water rises through shallow to deep in a sourced pool, after plant add/remove, a 120-tick random churn
  comparing every cell to the direct PTH-01/02 rules mid-tick and per tick, raw writes + InvalidateAll). First run
  failed to compile on the missing API; a mutation check (each of the three feeds disabled in turn) makes 2-4 of
  these tests fail. `PathGridTests` unchanged and green. check.sh: 186 passed, 78 skipped, 0 failed; Godot csproj
  0 warnings. perf.sh: 4 passed, 4 skipped (water budgets green after touching the water step).
- Decisions: ADR-023
- Golden: unchanged (the cache is not state).
- Perf: headless seed 1, 24,000 ticks: tick median 0.035 ms, p95 0.084 ms, 21,446 ticks/s, hash `0f27c8ee613dd61c`
  (same as M3-T8).
- Next: M4-T2 (A*). Query PathGrid freely; it syncs itself. M4-T10 load must call `PathGrid.InvalidateAll()`;
  M5-T2 construction-site blocking must add its own invalidation feed.

## M4-T2 — A* pathfinder (2026-09-26)
- Done: `Pathfinder.FindPath` (single and multi-goal) with A* keyed `(f, h, index)` (`PathHeap`, lazy decrease-key),
  octile + 2|dy| heuristic (min over goals), 20,000-expansion `TooFar` limit, pooled world-sized search arrays with
  a generation stamp. Neighbor/cost rules (PTH-04..08) in `PathMoves.From` so M4-T3 regions reuse them. Start must
  be standable (deep start allowed for WAT-14 flee); non-walkable goals are dropped. `LastExpanded` diagnostic.
- Tests: un-skipped `PathfinderTests.*` (8; all failed first on NotImplementedException). Added
  `PathfinderRuleTests` (9: step-down cost, climb/descent headroom, start==goal, invalid start, deep start paths out,
  unwalkable goals dropped, TooFar at exactly 20,000 expansions, path contiguity + cost = sum of legal steps).
  check.sh: 203 passed, 70 skipped, 0 failed; Godot csproj 0 warnings. perf.sh: 4 passed, 4 skipped.
  sim-reviewer: no required fixes; applied two doc clarifications.
- Decisions: ADR-024
- Golden: unchanged (search state is scratch).
- Perf: headless seed 1, 24,000 ticks: tick median 0.033 ms, p95 0.086 ms, 19,827 ticks/s, hash `0f27c8ee613dd61c`
  (unchanged). Informal A* on seed 1 (Release, warm, 200 pairs 60-75 cells apart in x/z, paths ~120 cells):
  median 1.1 ms, p95 ~4 ms, up to ~10k expansions on river detours. PTH-P1 budget is 1.5 ms p95.
- Next: M4-T3 (regions): flood fill with `PathMoves.From` on walkable cells. M4-T12 will likely need A* speedups
  (per-expansion cost ~0.4 us: `PathGrid.Flags` syncs the world log on every query, `CellOf` divisions,
  Int3-based neighbor walk) and must define the "~100-cell" sampling. The multi-goal heuristic loops over all
  goals per push; keep goal lists small in M4-T6/T7.

## M4-T3 — Regions (2026-09-26)
- Done: `Regions` full flood fill (BFS with `PathMoves.From`, ascending-index seeds, ids 1..n) rebuilt at ARCH-01
  step 11 when `PathGrid.WalkabilityVersion` changed (blocks, plants, `InvalidateAll`, shallow/deep crossings in
  standable cells only; `WaterGrid.WalkClassChanged` now carries a `deepChanged` flag) or after `MarkDirty()`.
  `Counters.RegionRebuilds` is counted. `RegionOf` returns `None` for non-walkable and out-of-bounds cells.
- Tests: un-skipped `RegionTests.*` (2) and `WorldInvariantTests.HubReachesRiverAndHill` (body written: nearest
  walkable dry cell with water within 2 cells of the hub, and the highest walkable cell within half the hill radius
  of the hill center, at least 8 above the entrance, share the hub's region). Added `RegionRuleTests` (deep water in
  a wall gap splits, a tree in the gap splits, no rebuild on quiet ticks or shallow-only water, out-of-bounds/solid
  None, and same region iff A* finds a path on 3 random rough terrains, 300 pairs each). All failed first on
  NotImplementedException. `PathGridCacheTests.Cache_ComputesEachCellOnceUntilInvalidated` setup adjusted (ADR-025).
  check.sh: 213 passed, 67 skipped, 0 failed; Godot csproj 0 warnings. perf.sh: 4 passed, 4 skipped.
- Decisions: ADR-025
- Golden: unchanged (regions are derived).
- Perf: seed-1 full rebuild ~4-5 ms Release (PTH-P2 budget 25 ms), 2 regions. Settled river: 14 rebuilds per 500
  ticks (one bank cell near the drains oscillates around half depth); without the standable filter it was 244.
  Headless seed 1, 24,000 ticks: tick median 0.032 ms, p95 0.071 ms, 18,336 ticks/s, hash `0f27c8ee613dd61c`
  (unchanged).
- Next: M4-T4 (agents and movement). JOB-06 region check: compare `Regions.RegionOf(agentCell)` against the target's
  adjacent cells; regions only update at tick end, so a same-tick dig is not visible until the next tick.
  M5-T2 construction blocking must bump `PathGrid.WalkabilityVersion` (via its invalidation feed).

## M4-T4 — Agents and movement (2026-09-26)
- Done: `AgentMovement` path following (PTH-15: 4 straight / 6 diagonal, +2 step up, +3 into a wet cell; the step is
  re-checked with the full `PathMoves` rules at segment start and just before entering; one repath per `MoveTo` to
  the chosen goal, then `MoveStatus.Failed`). `AgentSystem.MoveTo` (single/multi-goal) and `Tick` (ascending id,
  dead skipped). New `Agent.Move` / `Agent.Repathed`, hashed. `WorldFactory` spawns 5 dwarves (Dvalinn, Althjofr,
  Nyradr, Reginn, Hanarr) on the first dry walkable cells of a BFS from the hub entrance; `Create` now drains events
  at the very end. `ScenarioBuilder.Agent` works. `SimCounters.PathSearches` mirrors `Pathfinder.Searches`.
- Tests: moved the 4 M4-T4 placeholders into `AgentMovementTests` and wrote them, plus 4 more (whole path order and
  tick total, unreachable MoveTo, blocked mid-step never entered, movement in the hash); 8 total, all failed first
  on the missing API. Mutation check: dropping the entry re-check, the once-only repath limit, or the wade ticks
  each fails one test. check.sh: 221 passed, 63 skipped, 0 failed; Godot csproj 0 warnings. perf.sh: 4 passed,
  4 skipped. sim-reviewer: no required fixes; applied the dead-agent guard and an ADR note.
- Decisions: ADR-026
- Golden: regenerated (`UPDATE_GOLDEN=1`): five colonists are now part of the seed-1 state.
- Perf: headless seed 1, 24,000 ticks: tick median 0.033 ms, p95 0.072 ms, 18,488 ticks/s, 5 agents alive,
  hash `0c16a369fcd891de`. Water volume and active cells unchanged (agents are idle).
- Next: M4-T5 (WorldActions). M4-T6 GoTo steps: call `Agents.MoveTo(sim, agent, goals)` and read `agent.Move`
  (`Arrived`/`Failed`) each tick; `Failed` is the PTH-16 step failure. A mid-step `MoveTo` abandons the step.
  M4-T5 `PlaceBlock` should refuse a cell an agent stands in (`CanStep` assumes the agent's own cell is standable).
  M4-T10 must save `Move` and `Repathed`. Agents are not rendered yet (M4-T11).

## M4-T5 — WorldActions (2026-09-26)
- Done: every ARCH-07 action (`Dig`, `Chop`, `PlaceBlock`, `PickUp`, `PickUpFromStorage`, `Drop`, `DeliverTo`,
  `Consume`, new `Work(actor, WorkTarget)`) with the check order actor → id target → reach (26-neighborhood; any
  footprint cell for buildings) → preconditions, and no change on a non-Ok result. New `ItemPiles`
  (`Simulation.Piles`, one item per cell, sorted by cell index, emits `ItemPileChanged`, hashed). ECO-08 drop placement
  (target if standable and free, else a fixed radius-3 spiral of standable cells). Dig/PlaceBlock refuse cells that
  would remove an agent's floor or headroom (Cell or NextCell), a plant's floor, or a building's floor.
  `WorldActions` split into `WorldActions.cs` and `WorldActions.Storage.cs`.
- Tests: moved the 6 M4-T5 placeholders into `WorldActionsTests` with real bodies, plus 22 more (reach in 3D, dirt has no
  drop, agent/tree/building floors, dead/unknown actor, chop reach and bushes, PlaceBlock rules, pile merge, spiral
  cell, headroom drop, no free cell, storage pick-up/deliver caps, BLD-11 water, consume clamps, Work reach, piles in
  hash); 28 total, all failed first on the missing API (compile). check.sh: 249 passed, 57 skipped, 0 failed; Godot
  csproj 0 warnings. sim-reviewer: required fixes applied (ADR-027 written; Drop target must be standable, so piles
  never float from a drop). Also applied: check-order doc, dig under BuildingSolid refused, M4-T8 marker for BLD-10
  reservations.
- Decisions: ADR-027
- Golden: regenerated (`UPDATE_GOLDEN=1`): `StateHash` now includes the item pile set (empty on seed 1).
- Perf: n/a (no water/path code touched). Headless seed 1, 24,000 ticks: tick median 0.032 ms, p95 0.062 ms,
  21,091 ticks/s, 5 agents alive, hash `b05cc68ba432480e` (hash change only from the new pile field).
- Next: M4-T6 (JobBoard). Steps call `sim.Actions.*`; a non-Ok result is the JOB-08 failure. Reach uses `Agent.Cell`
  (not NextCell), so act only after `Move == Arrived`. `OutOfReach` is returned before any cell-content check.
  JOB-07 "drop carried on the agent's cell" can return `Blocked` (no free standable cell within radius 3): define a
  fallback. `Work` only checks reach; step progress lives on `Agent.StepProgress`. M4-T8: add BLD-10 reservations
  to storage actions and `ScenarioBuilder.Hub/Stock` (tests currently use `Buildings.PlacePrebuilt` + `Stored`).
  M4-T10: save `ItemPiles`. M5-T2 extends `DeliverTo` (sites, BLD-07 state change) and `Work` (construct progress).

## M4-T6 — JobBoard, job steps, selection, reservations, failure/retry (2026-09-26)
- Done: `JobBoard` (`Simulation.Jobs`: sorted jobs, monotonic ids, derived reservation tables for cells, pile items,
  storage in/out), `Job`/`JobStep`/`Reservation` plain data with JOB-05 priorities, `JobGoals` (GoTo goals: Exact /
  Reach / Building, ascending index), `JobRunner` called from `AgentSystem.Tick` (JOB-06 selection every 5 ticks while
  idle by priority → Manhattan → id, with region filter on every GoTo leg and reservations as preconditions; steps
  GoTo/Work/one per `WorldActions` call; JOB-08 failure → release, drop carried, 50-tick cooldown, cancel +
  `DigUnreachable` at 5; JOB-07 `AssignNeed` preemption; `Cancel` for DSG-06). Minimal `DesignationMap`
  (`Simulation.Designations`, DSG-01) for the unreachable mark; DSG-07 clear on dig completion. `AgentMovement.Halt`.
  `WorldActions.StoredCount/FreeCapacity` public. F3 overlay now shows open jobs by kind.
- Tests: moved the 6 M4-T6 placeholders into `JobBoardTests` with bodies, plus 6 more (cell reservation one claim at
  a time, pile/storage reservations as preconditions, need-job preemption drops cargo without a failure, cancel of a
  claimed job, 5-tick search throttle, board and designations in the hash). With the runner disabled 10 of 12 fail
  (the two API-level ones pass). check.sh: 261 passed, 51 skipped, 0 failed; Godot csproj 0 warnings. perf.sh: 4
  passed, 4 skipped. sim-reviewer: required fixes were the ADR (written) and this golden note; applied suggestions:
  need-kind check before posting, region check on every GoTo leg, `StoredCount` reuse.
- Decisions: ADR-028
- Golden: regenerated (`UPDATE_GOLDEN=1`): `StateHash` now includes the job board and the designation map (both empty
  on seed 1), so all four checkpoint hashes changed.
- Perf: headless seed 1, 24,000 ticks: tick median 0.032 ms, p95 0.053 ms, 23,233 ticks/s, jobs 0/0 (no posters yet),
  hash `a4f9af139f7155c6`. Screenshots re-rendered (Godot 4.6.2 .NET): unchanged, as expected (overlay is not shot).
- Next: M4-T7 (designations). Post dig jobs with `sim.Jobs.Post(JobKind.Dig, cell, [GoTo(cell), Work(cell, hardness),
  Dig(cell)], [Reservation.OnCell(cell)], priority 25 + DSG-04 bonus)`; Reach goals include the cell above the
  target, so JOB-09 needs a Dig-specific goal filter (e.g. a new GoalMode). Chop give-up needs a mark. DSG-06 cancel:
  `JobRunner.Cancel`. M4-T8: pile/storage reservations are held until the job ends; a haul failing after pick-up can
  leave an unclaimable job (re-post or cancel it). M4-T10: save the board (`Ids.Next`, all job fields), designation
  marks, agent `NextJobSearchTick/CurrentJob/StepIndex/StepProgress`, then `Jobs.RebuildReservations()`. M5-T5: a
  dead agent's claimed job must be released.

## G2 early answers (human, 2026-09-26)
- Q (palette / sRGB): should vertex colors be marked sRGB so Forward+ matches `data/palette.json` exactly?
  **No.** Keep the current look (lighter and greyer than the palette hex values). Do not set `VertexColorIsSrgb`
  or otherwise darken the scene.
- Q (screenshot renderer): should gate screenshots use the renderer the game plays in? **Yes.** Added M4-T13 to
  switch `screenshot.sh` to Forward+; it is now a dep of M4-GATE.

## M4-T7 — Designations: dig and chop (2026-09-26)
- Done: commands `DesignateDig(A, B)`, `DesignateChop(X0, Z0, X1, Z1)`, `CancelDesignation(A, B)`
  (`Commands/DesignationCommands.cs`) and a stateless `DesignationSystem` (ARCH-01 step 8) that posts one Dig job per
  exposed `Dig` mark (DSG-03) and one Chop job per marked tree. DSG-04 height bonus is recomputed every tick and capped at +4.
  JOB-09 `GoalMode.Dig` (never on top of the target; floors that are not designated come first). DSG-08: the job is
  not selected, and the Dig step waits (up to 200 ticks), while another agent stands on the block. An idle agent with no
  job that stands on a designated block steps aside. Chop give-up sets the new hashed `Plant.ChopUnreachable`.
  `DesignationMap` keeps a sorted index of marks. `AgentSystem.AnyHolds` is shared with `WorldActions`.
- Tests: `Scenarios/DigScenarioTests` (9 run + 2 with full bodies re-tagged `Skip = "M4-T8"` because they need hauling,
  ADR-029) and `DesignationTests` (12). All failed first on the missing API. Mutation checks: removing the JOB-09
  exclusion, step-aside, the height bonus, the exposure rule, or the select deferral each fails at least one test.
  check.sh: 282 passed, 47 skipped, 0 failed; Godot csproj 0 warnings. perf.sh: 4 passed, 4 skipped (unchanged).
  sim-reviewer: no rule violations. Its required fix is applied: the M4-T8 backlog line now names the two
  re-tagged tests. Also applied: DSG-02 skips plant floors, and a `DigUnreachable` mark on air is cleared. Deferred: stale
  `Dig` marks on solid, non-diggable cells (a manual bedrock mark is used by a JobBoardTests case), per-tick
  allocations, and moving `ChopTicks` into data.
- Decisions: ADR-029
- Golden: regenerated (`UPDATE_GOLDEN=1`): the plant hash now includes `ChopUnreachable`. With the field left out,
  the old golden still matched, so seed-1 behavior is unchanged.
- Perf: headless seed 1, 24,000 ticks: tick median 0.032 ms, p95 0.038 ms, 25,942 ticks/s, hash `d8a1e43aeeb5d540`
  (no commands, so jobs 0/0). One-off load probe (not committed): seed 1 with ~670 dig marks next to the hub plus
  every tree marked, 6,000 ticks: tick median 0.056 ms, p95 2.8 ms (A* searches, see M4-T12), 739 jobs done, 9 failed.
  No Godot code touched, so screenshots were not re-rendered.
- Next: M4-T8 (hauling). Un-skip `DigStone_EndsInHubStorage` and `Chop_MarkedTrees_LogsHauled` in
  `Scenarios/DigScenarioTests.cs` together with `HaulScenarioTests`. Implement `ScenarioBuilder.Hub/Stock` (both tests
  use `Hub`). Dig/chop piles land on the dug cell or the tree base. M4-T10 must save `Plant.ChopUnreachable` and the
  designation marks. Commands have tags `DesignateDig`, `DesignateChop` and `CancelDesignation` for the command log.
  M4-T11: draw `DigUnreachable` and `ChopUnreachable` in red.

## M4-T8 — Item piles, hub storage, hauling (2026-09-26)
- Done: stateless `HaulSystem` (`Jobs/HaulSystem.cs`, ARCH-01 step 9) keeps one Haul job per loose pile
  (`GoTo(pile) → PickUp(n ≤ 10) → GoTo(storage) → DeliverTo`, reserving the pile items and the storage room) while a
  complete storage accepts the item and has room; nearest storage by Manhattan to its entrance, ties by id. Unclaimed
  pile hauls are re-planned every tick or withdrawn (pile gone / no room), which also cleans up a haul that failed after
  its pick-up. `JobBoard.StorageRoom/StorageStock/ReservedInTotal` (BLD-10, total cap counts all items' reservations);
  `WorldActions.DeliverTo/PickUpFromStorage/Consume` honor other jobs' reservations (own job's count as its own);
  `WorldActions.Accepts` public. BLD-12 `BuildingSystem.Totals`. `ScenarioBuilder.Hub/Storage/Stock/Pile`.
- Tests: un-skipped `DigStone_EndsInHubStorage`, `Chop_MarkedTrees_LogsHauled`; new `Scenarios/HaulScenarioTests` (7:
  nearest storage + tie + BLD-11, storage full, ECO-08 drop spiral then haul, trips of 10, two haulers never overfill,
  failure after pick-up, totals), 4 `WorldActionsTests` (reserved room/stock, own reservation, warehouse total), 1
  `JobBoardTests` (total-cap reservations). Before wiring `HaulSystem` all 8 haul scenarios failed (conditions not
  reached / no haul job). Mutations: no re-plan → 2 fail; room without reservations → 1 fails. check.sh: 296 passed,
  42 skipped, 0 failed; Godot csproj 0 warnings. perf.sh: 4 passed, 4 skipped. sim-reviewer: no required fixes;
  applied: `Totals` counts complete storage only, and its doc notes the one-tick lag.
- Decisions: ADR-030
- Golden: unchanged (no piles on seed 1 without commands).
- Perf: headless seed 1, 24,000 ticks: tick median 0.031 ms, p95 0.038 ms, 26,390 ticks/s, hash `d8a1e43aeeb5d540`
  (unchanged). One-off probe (not committed): seed 1, one-layer dig of a 17×7 box next to the hub plus every tree
  chopped, 24,000 ticks: tick median 0.139 ms, p95 0.191 ms, 276 jobs done, 1 failed, 304 A* searches; the hub fills to
  its 100-log per-item cap and the other 125 log piles stay (no warehouse yet), 12 dig jobs stay unclaimed. No Godot
  code touched, so screenshots were not re-rendered.
- Next: M4-T9 (flee). M4-T10 must save item piles; haul jobs are plain board jobs (re-plan needs no extra state), and
  `Jobs.RebuildReservations()` now also rebuilds the total-in table. M4-T11: draw piles (`ItemPileChanged`). Pile
  hauls are recognised by kind + 4-step shape (`HaulSystem.IsPileHaul`): M5 haul-like jobs (refunds, BLD-14 pump
  hauls) must use a different shape or a marker. One job per pile means big piles are hauled one trip at a time.
  Hub per-item cap (100) is the limit on logs until warehouses (M5-T3).

## M4-T9 — Flee from deep water (2026-09-26)
- Done: `Pathfinder.FindFlee(start, 64)` does a breadth-first search in swim mode (`PathMoves.From(..., swim: true)`, in
  which deep standable cells count as passable) to the walkable cell with the fewest steps. `FleeRules.Tick` runs for
  each living agent before its job step. An agent on a deep cell claims a Flee need job (priority 200; it preempts
  any job, including Drink/Eat) and follows the found path at once, with swim moves. A blocked swim step fails the
  job, and the agent searches again at once. With no path it is `AgentState.Trapped`: its job is released and its
  stack dropped, it takes 1 damage per tick and searches again every 5 ticks. At health 0 it dies with
  `DeathCause.Drowned` through the new `AgentSystem.Kill`, which releases the job, drops the stack and emits
  `AgentDied`. JOB-07/08: need jobs that fail or are preempted are removed from the board, not left for a retry.
- Tests: `Scenarios/FloodScenarioTests` has 11 tests: the 2 placeholders with bodies, plus preemption with a cargo
  drop, ignoring the board while fleeing, a blocked flee path, water receding, the Trapped state, and the throttle
  regression (5 flood ticks). `FleeSearchTests` has 4: swimming through deep water where A* gives NoPath, exactly
  64 steps found but 65 NoPath, nearest by steps, and start rules. All failed first: the API was missing, then
  there was no flee/drown behavior. Mutations: keeping failed need jobs on the board makes 1 test fail; the old
  shared throttle makes 4 fail. check.sh: 312 passed, 40 skipped, 0 failed; Godot csproj 0 warnings. perf.sh:
  4 passed, 4 skipped.
- sim-reviewer found 1 required fix, now applied: the first flee search was delayed by the JOB-06 idle search
  throttle, which shared `NextJobSearchTick`. Only Trapped agents are throttled now (new `AgentState.Trapped`).
  Also applied: an `AssignNeed` null result returns without damage, and ADR-031 notes that a blocked drop on death
  loses the stack.
- Decisions: ADR-031
- Golden: unchanged. No agent reaches deep water on seed 1 without commands.
- Perf: headless seed 1, 24,000 ticks: tick median 0.031 ms, p95 0.043 ms, 25,931 ticks/s, hash `d8a1e43aeeb5d540`
  (unchanged). No Godot code touched, so screenshots were not re-rendered.
- Next: M4-T10 must save `Health`, `Death`, `State` (including Trapped), `NextJobSearchTick` and Flee jobs. A saved
  in-progress Flee job resumes on its saved swim path (`StepProgress = 1`). `AgentMovement.Advance` needs
  `swim: true` for Flee, which `JobRunner` already passes. M4-T11: show Trapped/drowning agents. M5-T5: use
  `AgentSystem.Kill(sim, a, Starved/Dehydrated)`, which releases the claimed job. Need jobs are removed on failure,
  so the needs system must re-post them. Drink/Eat preemption goes through `AssignNeed`, and Flee preempts them.
  M5-T2: construction footprints are standable but not walkable, so swim mode would let a fleeing agent cross one.

## M4-T10 — Save/load v1 (2026-09-26)
- Done: `SaveGame.Save/Load` (`Save/SaveGame.cs`, `SaveGame.Entities.cs`, `SaveIo.cs`, `CommandCodec.cs`). SAV-01
  binary format: magic `CSAV`, version 1, marked sections in spec order (header incl. a "regions built" flag, RLE blocks,
  RLE water levels + active set + sources/drains + stats, plants, buildings, storage, piles, designations, alive agents,
  jobs with steps and reservations, id allocators, command log, pending commands). Load restores through `internal
  Restore` hooks (no events), then clears the change log, marks all chunks dirty, `PathGrid.InvalidateAll()`, rebuilds
  regions (only if the saved game had them), `Jobs.RebuildReservations()`, and drains events. Bad magic, version,
  truncation or corrupt values throw `InvalidDataException`. A command with no codec throws `NotSupportedException` on save.
- Tests: `SaveLoadTests` has 10 run + `SaveSize_Day5_Under3MB` still `Skip = "M6-T7"`. The 3 placeholders now have
  bodies: RoundTrip_HashEqual (seed 1, 1000 ticks, dig active; also byte-identical re-save), RoundTrip_FutureEqual (1000
  ticks, chop command after load, hash every 100), and WrongVersion_FailsClearly. New tests cover: bad magic and
  truncation, SAV-06 dead agents dropped, a save mid-flee (swim path), piles/stock/designations/command log/pending
  command, derived data (reservations, regions), a save before the first tick, and an unknown command tag. All failed
  first on `NotImplementedException`. Mutations: skipping the region rebuild or the reservation rebuild fails
  DerivedData; always rebuilding regions fails SaveBeforeFirstTick. check.sh: 322 passed, 37 skipped, 0 failed; Godot
  csproj 0 warnings. perf.sh: 4 passed, 4 skipped.
- sim-reviewer: its one required fix is applied. A save taken before the first tick used to load with regions built,
  so tick 1 differed; the new header flag fixes that. Also applied: block bytes and item ids (1-based) are validated,
  `FormatException` and `IOException` are wrapped, and the codec uses `nameof` tags.
- Decisions: ADR-032
- Golden: unchanged (no sim behavior change).
- Perf: headless seed 1, 24,000 ticks: tick median 0.032 ms, p95 0.035 ms, 26,406 ticks/s, hash `d8a1e43aeeb5d540`
  (unchanged). One-off probe (not committed): seed-1 save at day 5 is 29,376 bytes (SAV-05 budget 3 MB); save ~20 ms,
  load ~52 ms (Debug test run). No Godot code touched, so screenshots were not re-rendered.
- Next: M4-T11. F5/F9 should call `SaveGame.Save/Load` between ticks, swap in the new `Simulation`, and rebuild every
  renderer from scratch (load drains events; all chunks are marked dirty, so `ChunkDirty` fires on the first tick).
  Every later task that adds sim state must extend `SaveGame` + hash in the same task. New commands need a
  `CommandCodec` entry, or saving throws. Farm tiles (M6-T2), moisture (recomputed, M6-T1) and weather (M6-T4) must add
  sections and bump `FormatVersion`. M5-T2 construction-site blocking: if it is derived, rebuild it in `AfterLoad`.
  Test-only `SetBlockCommand` has no codec.

## M4-T11 — Godot: agents, piles, designations rendering; dig/chop/cancel tools; colonist panel; F5/F9 (2026-09-26)
- Done: View logic lives in ViewCore, with no Godot types.
  - `Entities/` has `AgentVisuals` (look, lerped position, slice), `PileMesher` (cubes, hover label), `DesignationMesher` (dig cubes, chop rings, red when unreachable, change signature) and `EntityColors`.
  - `Tools/ToolController` covers Dig (G), Chop (C) and Cancel (Z) drags turned into commands.
  - `Hud/ColonistPanelModel` builds the panel rows and activity text.
  - `Persistence/QuickSave` does F5/F9 with a temp file and error messages.
  - `MeshShapes.AddBox`, and `OrbitRig.CenterOn`.
  - The Godot side is thin: `AgentRenderer`, `PileRenderer`, `DesignationRenderer`, `ToolPreview`, `Hud` (toolbar, toast, pile hover label), `ColonistPanel` ("Dwarves"; clicking a row centers the camera), and `GameRoot` split into `.Input.cs` and `.Save.cs`.
  - `AttachSimulation` rebuilds every renderer when F9 loads a save. The F3 overlay moved to the top right.
  - The screenshot harness has `--script none|digchop` (`SCRIPT=digchop ./scripts/screenshot.sh`).
- Tests: 35 new view tests (`EntityViewTests`, `ToolAndPanelTests` with tool/panel/quick-save tests, `ScreenshotScriptTests`).
  They first failed on the missing namespaces. check.sh: 357 passed, 37 skipped, 0 failed. Godot csproj: 0 warnings.
- Screenshots (Godot 4.6.2, opengl3, `SCRIPT=digchop`, looked at):
  - `artifacts/screens/t400/hub.png` shows translucent dig marks on the half-dug pit, dwarves (white capsules) inside it, and orange chop rings around about 25 trees. All panel rows say "Digging".
  - `artifacts/screens/hub.png` (tick 1200) shows the finished pit, most trees felled, and log piles scattered. The panel reads "Hauling log" or "Felling a tree".
- Probe (seed 1, digchop):

  | Tick | Dig marks | Marked trees | Piles | Jobs done | Jobs failed | Logs in hall |
  |---|---|---|---|---|---|---|
  | 300 | 53 | 23 | – | 37 | – | – |
  | 1200 | 0 | 3 | 18 | 110 | 1 | – |
  | 2400 | 0 | 0 | 0 | 136 | 1 | 92 |

- **Finding (sim, not fixed here):** a 10x7x5 pit dug top-down (DSG-04) leaves no ramp. All 5 dwarves ended on the pit floor (y=19, region 2), cut off from the Great Hall (region 1). Chop jobs were then never taken: 23 trees stayed marked and jobs done stayed at 279. Dig jobs never check that the digger can still get out. Raise this at G2, or give a later task a rule (for example, keep a stair or ramp cell, or refuse a dig that would strand the worker).
- Decisions: ADR-033
- Golden: unchanged (no Aurvangar.Sim code changed).
- Perf: perf.sh 4 passed, 4 skipped. Headless seed 1, 24,000 ticks: median 0.031 ms, p95 0.034 ms, 26,838 ticks/s, hash `d8a1e43aeeb5d540` (unchanged).
- Next:
  - M4-T13 switches screenshot.sh to Forward+ and must keep `--script` / `SCRIPT`.
  - The G2 gate can use `SCRIPT=digchop` (`TICKS=400` shows the marks; the default 1200 shows the piles).
  - The headless runner has no dig+chop script: `ScreenshotScripts` is in ViewCore, and Headless references only Sim.
  - New sim-visible states (for example, construction sites in M5) need a look in `AgentVisuals` and `ColonistPanelModel.Activity`.
