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
