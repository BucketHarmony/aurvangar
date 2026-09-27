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

## M4-T12 — Path and region perf (2026-09-26)
- Done: A* and region fill now use `PathMoves.Steps`, which applies the PTH-04..08 rules to raw coordinates, with
  the same moves in the same order. It reads the new `PathGrid.FlagsAt(x, y, z)`, which checks bounds inline and
  does not sync. A search or rebuild syncs the world change log once instead of on every flag read.
  `PathMoves.From` wraps `Steps`, so every caller still shares one rule implementation.
- Tests: `Perf/PathPerfTests` covers `AStar_P95_100CellPaths` and `RegionRebuild_Seed1`. Both placeholders were
  moved out of `OtherPerfTests` and given bodies. Pair sampling is deterministic (ADR-034): Rng(1234), paths of
  90..110 cells in the largest region. Before the change, A* failed for the right reason: p95 1.681 ms against the
  1.5 ms budget. The region test checks both warm and cold rebuilds. check.sh: 357 passed, 37 skipped, 0 failed;
  Godot csproj 0 warnings. perf.sh: 6 passed, 2 skipped.
- Decisions: ADR-034
- Golden: unchanged (no behavior change).
- Perf (Release, 3 runs):
  - PTH-P1: A* median ~0.31 ms, p95 0.75-0.81 ms (budget 1.5), max ~1.05 ms, max expanded 5,153.
  - PTH-P2: region rebuild warm median ~2.1-2.2 ms, cold ~3.4-3.6 ms (budget 25).
  - Headless seed 1, 24,000 ticks: median 0.032 ms, 24,440 ticks/s, hash `d8a1e43aeeb5d540` (unchanged).
  - No Godot code was touched, so no screenshots were taken.
- Next: M4-T13 (screenshot.sh to Forward+). Hot search loops should call `PathMoves.Steps` or `PathGrid.FlagsAt`,
  and must call `PathGrid.SyncWorldChanges()` once first. M5-T2 construction-site blocking belongs in
  `PathGrid.Compute`, so both paths see it. The pit-trap issue from M4-T11 is still open for G2.

## M4-T13 — Screenshots render with Forward+ (2026-09-26)
- Done: `scripts/screenshot.sh` now runs Godot with `--rendering-method forward_plus` and no `--rendering-driver`
  (was `--rendering-driver opengl3`, Compatibility). Godot reports "Vulkan 1.4.329 - Forward+" on this machine.
  `SEED`/`TICKS`/`SHOTS`/`OUT`/`SCRIPT` are unchanged. The separate `artifacts/screens/forward_plus/` path is
  dropped (a stale copy from M3-T9 is still on disk in the ignored `artifacts/`). docs/testing.md "Screenshot
  presets" now says shots use Forward+ and lists the env options. Vertex-color sRGB handling is untouched (G2 early
  answer). `SceneLightingTests` and CLAUDE.md do not mention the renderer, so they needed no change.
- Tests: added `View/ScreenshotRendererTests` (4): the script uses Forward+ on the default driver with no opengl3 or
  gl_compatibility; it keeps the `SCRIPT`/`TICKS` options; project.godot does not override the Forward+ renderer;
  docs/testing.md says Forward+. Two failed first for the right reason (no `--rendering-method forward_plus` in the
  script; no "Forward+" in the doc). check.sh: 361 passed, 37 skipped, 0 failed; Godot csproj 0 warnings.
  perf.sh: 6 passed, 2 skipped.
- Screenshots (Godot 4.6.2 .NET, Forward+/Vulkan, seed 1, looked at):
  - Default, 1200 ticks: `artifacts/screens/{overview,river,hub,slice}.png`. They look like play: lighter, greyer
    greens than the old Compatibility shots; shadowed faces and tree shadows are dark green-grey, never black; the
    river is a translucent blue-grey that deepens mid-channel; the slice shows brown dirt with grey stone patches
    and the cut faces. The hub is a small grey block with the idle dwarves next to it.
  - `SCRIPT=digchop TICKS=400`: `artifacts/screens/t400/{hub,river}.png`. The half-dug pit has translucent yellow
    dig marks and 5 white dwarves inside it, with orange chop rings around about 25 trees. The panel reads
    "Digging" x4 and "Felling a tree".
  - `SCRIPT=digchop` (1200): `artifacts/screens/digchop/hub.png`. The pit is finished and most marked trees are
    felled. Log piles show as small brown cubes that are hard to see at this zoom. The panel reads "Felling a tree"
    and "Hauling log".
- Decisions: ADR-035 (supersedes the Compatibility-screenshot consequence of ADR-022).
- Golden: unchanged (no sim change).
- Perf: n/a (no sim change). perf.sh green.
- Next: M4-GATE (G2). Use `SCRIPT=digchop` renders, which are now Forward+. The pit-trap issue from M4-T11 is still
  open. On Linux/xvfb, screenshots now need a Vulkan driver (e.g. lavapipe).

## M4-GATE — HUMAN-GATE G2: colonists at work (2026-09-26)

**Status: waiting for human review. The gate box in BACKLOG.md is NOT checked; check it after review.**
All M4 tasks (T1..T13) are checked. No M5 work has started.

### What changed for the gate
- The headless runner can now run the dig + chop script: `./scripts/run-headless.sh --seed 1 --ticks 2400
  --report-every 300 --script digchop` (ADR-036). It reuses the screenshot harness's `ScreenshotScripts` (ViewCore,
  Godot-free), so the stats and the screenshots describe the same work: a 14x9, 2-deep pit 3 cells east of the hub,
  and every tree within 24 cells of the hub marked for chopping. It prints a `work:` line per report and a
  `summary:`. Without `--script`, output and hash are unchanged (`d8a1e43aeeb5d540` at 24,000 ticks).
  docs/testing.md "Scripted play" documents it. No sim code changed; golden unchanged.

### Build and test results (this run)
- `./scripts/check.sh`: OK. 361 passed, 37 skipped (acceptance tests for M5+), 0 failed. Godot csproj 0 warnings.
- `./scripts/perf.sh`: OK. 6 passed, 2 skipped (M6-T1 moisture, M6-T7 full tick at day 5).

| Budget | Measured (Release, this machine) | Limit |
|---|---|---|
| WAT-P1 128x128 (15,494 active) | median 1.13 ms, p95 1.19 ms | 4 ms |
| WAT-P1 192x128 (>= 23,048 active) | median 1.72 ms, p95 1.84 ms | 4 ms |
| WAT-P2 seed-1 river, tick 1200 | 699 active cells, step median 0.049 ms | 3000 cells |
| MESH-P1 chunk (3,0,2), 581 quads | median 0.63 ms, p95 0.69 ms (sliced: 0.55 ms) | 6 ms |
| PTH-P1 A*, 200 paths of 90..110 cells | median 0.31 ms, p95 0.80 ms, max 1.02 ms (5,153 expanded) | 1.5 ms p95 |
| PTH-P2 region rebuild seed 1 | warm median 2.18 ms, cold 3.47 ms | 25 ms |

Nothing is within 20% of its limit (PTH-P1 p95 is at 53%).

### Headless run: seed 1, `--script digchop`, 2400 ticks (1 day)

| Tick | Dig marks left | Trees marked | Piles (items) | Carried | Stored (logs) | Jobs done | Failed | Path searches | Idle |
|---|---|---|---|---|---|---|---|---|---|
| 300 | 53 | 23 | 0 | 0 | 0 | 37 | 1 | 46 | 0 |
| 600 | 5 | 22 | 1 (4) | 0 | 0 | 86 | 1 | 95 | 0 |
| 900 | 0 | 14 | 9 (36) | 0 | 0 | 99 | 1 | 109 | 0 |
| 1200 | 0 | 3 | 18 (72) | 8 | 0 | 110 | 1 | 122 | 0 |
| 1500 | 0 | 0 | 7 (28) | 16 | 48 | 125 | 1 | 150 | 0 |
| 1800 | 0 | 0 | 1 (4) | 4 | 84 | 134 | 1 | 163 | 3 |
| 2100 | 0 | 0 | 0 | 0 | 92 | 136 | 1 | 164 | 5 |

Summary: **136 jobs completed** (90 digs + 23 chops + 23 hauls), **1 job failure** (before tick 300; the job was
retried and completed, nothing was left unreachable; the cause was not traced), **92 logs hauled** into the Great
Hall (23 trees x 4 logs, one pile per trip), **164 path searches**, 202 region rebuilds, 0 trapped agents, all 5
dwarves in one region at the end. The pit is dirt and grass only, so digging produced no items (only stone drops
items). Everything is done by ~tick 2000; the 5 dwarves are then idle. The run is deterministic: two runs gave hash
`a182db6b82bdf66f`. Continued to 24,000 ticks: same totals, hash `debf00e425c86e2b`, 17,076 ticks/s.
Tick cost while working: median 0.07-0.15 ms, but **p95 2.3-3.0 ms**. Each dig or chop triggers a region rebuild
(~2 ms). That is well under SIM-P1 (8 ms median at day 5), but it is the biggest cost per tick right now.

### Screenshots (Godot 4.6.2 .NET, Forward+/Vulkan, seed 1, looked at)
- Default, 1200 ticks, no script: `artifacts/screens/{overview,river,hub,slice}.png`. Same as M4-T13: 5 idle dwarves
  next to the hub. The overview shows the whole map with the river, and the panel lists all 5 dwarves as "Idle".
- `SCRIPT=digchop TICKS=400`: `artifacts/screens/g2/t400/{overview,river,hub,slice}.png`. In `hub.png` the pit is
  about half dug (brown floor). The remaining cells have translucent yellow dig marks, and 4 white dwarves are
  working inside the pit. Orange chop rings circle about 25 trees. The panel reads "Digging" x4 and "Felling a
  tree". One tree stands in the pit area (see issue 2).
- `SCRIPT=digchop` (1200): `artifacts/screens/g2/t1200/{overview,river,hub,slice}.png`. In `hub.png` the pit is
  finished and most marked trees are gone. Dwarves are out in the field, and the panel reads "Felling a tree" x3
  and "Hauling log" x2. **The log piles are tiny brown specks at this zoom (distance 32).** A single grass-topped
  column is left standing in the pit where the tree was.

### Open issues
1. **Pit trap (from M4-T11).** Digging is top-down with no exit rule. In a 10x7x5 pit all 5 dwarves ended on the
   floor, cut off from the hall (separate region). Chop jobs then went untaken for good. The 2-deep gate pit does
   not trap anyone, because a 1-step climb is allowed. The problem only shows with 3+ deep digs.
2. **Tree floors are skipped.** DSG-02 (as implemented) never marks the cell under a plant, because `WorldActions.Dig`
   refuses a plant's floor. Felling the tree later does not re-mark it, so a drag over a wooded area leaves one-cell
   pillars (visible in `g2/t1200/hub.png`). The player has to drag the dig again after chopping.
3. **Log piles are hard to see** at the default hub zoom. They are drawn as small brown cubes sized by count.
4. **Hub capacity is 100 per item** until warehouses arrive (M5-T3). The gate run stores 92 logs. A larger chop
   area would fill the hall, and the leftover logs would stay in piles.
5. **Region rebuild per dig/chop** (~2 ms) is the main cost of a busy tick (p95 2-3 ms). This is fine for now; M6-T7
   (SIM-P1) will measure it at day 5.
6. One job failure in the run is untraced. It was retried and completed.

### Questions for the human
1. **Pit trap**: how should digging avoid stranding dwarves? Options:
   (a) **Auto-stair**: when a dig would leave the designated area with no walkable way up to the rest of the colony,
   keep one cell per level as a step (a 1-high staircase along one wall). Dwarves can always get out, and the pit
   loses a few cells.
   (b) **Refuse stranding digs**: a dig job is not taken if, after it, the digger's standing cell would be in a
   different region from the hub. That job waits, or is marked unreachable (red) once the rest is done. It is
   simple and safe, but it can leave the last layer undug.
   (c) **Leave it to the player**: no rule, but show a warning (for example, the pit marks turn red when a dwarf is
   trapped). This is closest to DF, but it is easy to lose all dwarves in the POC.
   My recommendation is (b) now, with (a) as a later nicety.
2. **Tree floors in a dig drag**: should the dig designation also mark cells under trees, and dig them once the
   tree is gone (the dig job simply waits until the plant is removed)? Or keep them unmarked, as now?
3. **Log pile visibility**: make piles bigger or brighter (for example, a fixed-size marker plus a count label), or
   leave them until an item art pass?
4. **Hauling while storage is full**: when the hall hits 100 of an item before warehouses exist, should the logs
   stay in piles (current behavior), or should haul jobs stop being posted with a HUD notice?

Next after approval: M5-T1 (building definitions, rotation, placement validation), plus any tasks the answers add
(for example, a pit-exit rule before M5).

### G2 answers (human, 2026-09-26)
- Q1 (pit trap): **(b) refuse stranding digs.** A dwarf does not take a dig that would cut it off from the Great
  Hall; the dig waits or turns unreachable. Added M4-T14.
- Q2 (tree floors): **yes.** Dig drags also mark cells under trees; the dig waits until the tree is gone. Added M4-T15.
- Q3 (log piles): **make them bigger.** Fixed-size marker plus a count label. Added M4-T16.
- Q4 (full storage): **keep piles.** When storage is full, items stay in piles on the ground. No change.
- Gate G2 closed (M4-GATE checked). Next: M4-T14, M4-T15, M4-T16, then M5-T1.

## M4-T14 — Digs never strand the digger (2026-09-26)
- Done: G2 answer 1b, new spec rule DSG-09 (JOB-09 cross-reference). A dig is never taken or finished from a stand
  cell that it would cut off from the Great Hall. `Paths/DigTrial` answers "would digging block T cut stand S off
  from the hall's reach cells?":
  - It starts with a cached local test. Only the cell on top of T can be lost, so if that cell is not walkable, or
    its move neighbors meet again within 512 cells on the what-if view, the dig cuts nothing.
  - Otherwise an exact flood runs from both sides at once, so a stranded pocket is found in about twice its size.
  - `PathMoves.Steps` is now generic over a struct `IMoveCells` view (`LiveCells` / `DigTrialCells`), so the
    what-if flood uses the same move rules as A* and regions.
  - `Jobs/DigStrand` supplies the anchors (the hall = the lowest-id complete `hub`; with no hall the rule is off).
  - Dig goal cells drop stranding stand cells, so selection and GoTo never use them. `JobRunner` checks again at Work
    start and at the Dig step; a dig that would now strand goes back to the board with a cooldown and no failure.
  - `DesignationSystem` turns stranding digs DigUnreachable (and removes their jobs) once no dig is claimed and no
    open dig has a safe stand cell in a living agent's region.
- Review (sim-reviewer): three fixes applied.
  - The MaySplit cache is also keyed on the new `PathGrid.DeepVersion`, which counts every shallow/deep crossing, so
    a loaded game and a continuous run give the same answers.
  - A safe stand cell in no agent's region no longer blocks the give-up.
  - Mid-tick with stale regions, a live flood checks "apart already?" first.
- Tests: `Scenarios/StrandScenarioTests` (4) and `DigTrialTests` (4).
  - The three original scenarios failed first for the right reason: a dwarf in a region other than the hall's, at
    tick 937 / 1235 / 1271.
  - `UnreachableDigElsewhere_DoesNotBlockGiveUp` fails without the review fix.
  - In the seed-1 10x7x5 pit (+ chop radius 24), all 5 dwarves stay in the hub region on every tick and every marked
    tree outside the pit is felled. 347 of 350 cells are dug and 2 turn red.
  - One tree inside the pit box is left on an undug pillar and cannot be reached; that is M4-T15 (tree floors).
  - check.sh: 369 passed, 37 skipped, 0 failed; Godot csproj 0 warnings.
- Decisions: ADR-037.
- Golden: unchanged (no script in the golden run). Headless seed 1, 24,000 ticks: hash `d8a1e43aeeb5d540`
  (unchanged), 25,314 ticks/s. `--script digchop` 2400 ticks: hash `a182db6b82bdf66f` (unchanged; the 2-deep gate
  pit never triggers the rule), 136 jobs, 1 failure, 92 logs stored.
- Perf: perf.sh 6 passed, 2 skipped. PTH-P1 A* p95 0.77-0.81 ms (budget 1.5). PTH-P2 warm median 1.95-2.08 ms
  (budget 25); one cold-JIT run read 4.1 ms. No Godot code changed, so no screenshots were taken.
- Next: M4-T15 (tree floors). Dig marks under trees will make pit pillars go away. The pit test currently skips trees
  inside the pit box, and could drop that exclusion once M4-T15 lands. Only the digger is protected (the human's
  rule); another dwarf could still in principle be cut off by someone else's dig. New dig-like mutations (M5
  construction footprints) should consider `DigStrand` if they remove floors.

## M4-T15 — Dig drags mark tree floors (2026-09-26)
- Done: G2 answer 2. `DesignateDig` now marks the floor under a plant (tree or bush). Its dig job is not posted
  while the plant stands; once the tree is felled (or the plant removed) the next designation tick posts it as usual,
  and ADR-037's strand rule, DSG-08 and JOB-09 apply unchanged. DSG-02/03 updated.
- Found while testing: with only mark + wait, the digs around a standing tree went on, so in a pit the tree ended on a
  pillar 2+ levels above any standable cell, out of chop reach (the seed-1 10x7x5 pit left tree (47,23,59) standing).
  New `Designations/TreeFloors`: while a tree marked for chopping (not given up) stands on a Dig-marked floor F, a
  mark at Chebyshev distance r >= 1 from F with y <= F.y - r gets no job (an unclaimed one is withdrawn). What may be
  dug meanwhile forms 1-high steps down from the tree. Derived each tick; no new sim state, save or hash fields.
- Tests: `DesignateDig_SkipsPlantFloor` replaced by `DesignateDig_MarksPlantFloor_JobWaitsForPlant` (the old test
  encoded the behavior the human changed), plus `PlantFloor_UnchoppedTree_WaitsWithoutFailures`.
  New `Scenarios/TreeFloorScenarioTests`: a 6x6x2 wooded box with dig + chop leaves no pillar (4 trees, 0 failures);
  a 9x9x4 pit with a tree in the middle, chop marked 30 ticks after the dig, fells the tree and digs its floor.
  `StrandScenarioTests.Seed1_DeepPit_*` no longer excludes trees inside the pit box. All failed first for the right
  reason (marks missing; the pit tree left standing), and the three scenarios fail again with the hold disabled.
  check.sh: 372 passed, 37 skipped, 0 failed; Godot csproj 0 warnings.
- Decisions: ADR-038.
- Golden: unchanged (the golden run has no script). Headless seed 1, 24,000 ticks: hash `d8a1e43aeeb5d540`
  (unchanged), 26,613 ticks/s.
- `--script digchop`, 2400 ticks: hash changed `a182db6b82bdf66f` -> `edb36219b6a838fa`, because the one tree inside
  the gate pit now has its floor dug: 91 of 91 cells dug (was 90, with a pillar), 23 trees felled, 92 logs stored,
  137 jobs, 168 path searches, 0 trapped, 2 failures. Both failures were traced with temporary logging (removed): GoTo
  step failures (PTH-16, the path changed while walking) at tick 48 and tick 625; both jobs were retried and done.
  The tick-48 one is the "untraced" failure from the G2 report.
- Perf: perf.sh 6 passed, 2 skipped. No Godot code changed, so no screenshots were taken.
- Next: M4-T16 (bigger piles). A log pile at a felled tree's base stays in place when the floor below is dug
  (loose piles do not fall, as with stone drops); it is still hauled from a neighbouring cell. A chop is not
  prioritised over digs; the holds keep the order safe.

## M4-T16 — Bigger, readable item piles (2026-09-26)
- Done: G2 answer 3, VIEW-10 updated. `PileMesher` (ViewCore) now builds `PileMarker`s: one fixed-size marker per pile
  whatever its count (a 0.74 x 0.45 box in the item color with a lighter cap), a count label text + anchor, and a
  post depth. A pile over a dug floor (piles do not fall) gets a thin darker post down to the floor below (max 8
  cells), and hovering that floor finds the pile. Godot: `PileRenderer` draws the mesh plus pooled fixed-size
  `Label3D` count labels ("4"); GameRoot shows them while camera distance <= 80 (default 60, hub preset 40) and
  hides them at the overview (120). Answer 4 (full storage: piles stay) needed no change.
- Tests: new `View/PileViewTests` (9): fixed size for 1 vs 500 items, item + cap colors and winding, label text and
  anchor, slice hiding, post to the floor below, post cap over a deep shaft, hover on a pile, hover through a
  floating pile's post, label zoom rule. They failed first (API missing). Replaced the two old cube-per-5-items tests
  in `EntityViewTests` (they encoded the look the human changed; ADR-039). check.sh: 374 passed, 37 skipped, 0
  failed; Godot csproj 0 warnings.
- Screenshots (Forward+, looked at): `SCRIPT=digchop TICKS=1200 OUT=artifacts/screens/t16/digchop1200`. In `hub.png`
  and `river.png` every felled tree's log pile is a clearly visible tan crate with a white "4" above it; the pile
  left over the dug pit (the M4-T15 tree floor) floats at ground level on a thin post down to the pit floor.
  `overview.png` has no labels, piles are small dots. `slice.png` hides piles above the slice and labels the one at
  the slice level. Default shots (`artifacts/screens/*.png`, no script) are unchanged: no piles, 5 idle dwarves.
- Decisions: ADR-039.
- Golden: unchanged (no sim change). No headless rerun needed (view only).
- Perf: perf.sh 6 passed, 2 skipped (view-only change).
- Next: M5-T1. Pile count labels are one `Label3D` per visible pile, rebuilt only on `ItemPileChanged` or a slice
  change; if M5+ produces hundreds of piles, consider hiding labels by distance per pile.

## M5-T1 — Building definitions, rotation, placement validation (2026-09-26)
- Done: `BuildingSystem.CanPlace` (new `BuildingSystem.Placement.cs`) returns a `PlacementResult` reason code. The
  checks run in a fixed order: BadRotation, PrebuiltOnly, OutOfBounds, Overlaps, FootprintBlocked, NotOnGround,
  EntranceBlocked, NeedsWaterEdge.
  - New `BuildingShape` holds BLD-01 geometry: the footprint, bottom layer, entrance, and the pump's intake front and
    intake cells, for any def/origin/rotation. `Building` delegates to it and gains `Covers`.
  - `BuildingSystem` now takes `PlantSystem` and `PathGrid`. It gains `BuildingAt(cell)` (lowest id first) and
    `TryPlaceBlueprint`, which adds a `Blueprint`-state building and writes no blocks. That is the state part of
    BLD-05; the command, jobs and event come in M5-T2.
  - New data field `stackable` in buildings.json (true for the levee only; `BuildingDef.Stackable`, default false).
  - ContentDb now rejects an unknown `placement` value and an entrance that is inside the footprint or off y = 0.
  - buildings.md BLD-02/03/04 and the schema are updated.
- Tests: the 7 placeholders moved to `BuildingPlacementTests.cs` with real bodies, plus
  `FootprintBlocked_BySolidOrPlant_WaterAllowed` and `Blueprints_SurviveSaveLoad_AndStillReserveTheirFootprint`
  (9 in all). All failed first with NotImplementedException. check.sh: 383 passed, 30 skipped, 0 failed; Godot
  csproj 0 warnings.
- Review (sim-reviewer): no required fixes. Applied two of its suggestions:
  - Stacking is allowed only on a same-type building in state Blueprint or Complete, and only a Complete stackable
    building counts as ground.
  - Added a save round-trip test for blueprints.
- Decisions: ADR-040. It covers the check order and the `stackable` flag. A stacked levee may use the standable
  cell one level below its entrance, so levees stack two high from open ground. For the pump edge, both the front
  cell and the intake cell must be non-solid; water is not required at placement.
- Golden: unchanged. Headless seed 1, 24,000 ticks: hash `d8a1e43aeeb5d540` (unchanged), 25,233 ticks/s.
- Perf: perf.sh 6 passed, 2 skipped (no water/path code touched). No Godot code changed, so no screenshots.
- Next (M5-T2):
  - Add a `PlaceBuilding` command (plus its CommandCodec entry) that calls `TryPlaceBlueprint`, publishes
    `BuildingPlaced`, or on failure publishes `CommandRejected` with the reason.
  - When a stacked building's entrance cell is not standable, the builder stands one level below it (ADR-040).
  - BLD-04 ordering (an upper levee waits for the one below) is not enforced yet.
  - Placement ignores dig designations on the ground under a blueprint; M5-T2 may want to guard that.
  - `BuildingAt` and `Covers` scan every building; add an index if they end up in per-tick code.

## M5-T2 — Construction flow (2026-09-26)
- Done: new commands `PlaceBuilding` and `Deconstruct` (both in the CommandCodec).
  - `Construction` (Construction.cs, Construction.Jobs.cs) runs at ARCH-01 step 7. It keeps the Deliver jobs
    (BLD-06, loads of at most 10 sourced from storage), one Construct job, and one Deconstruct job per building, and
    cancels stray ones.
  - `WorldActions.Construction.cs`:
    - `DeliverTo` on a site. The first delivery starts construction (BLD-07): the footprint is blocked, agents and
      piles are moved out, and the last delivery posts the Construct job.
    - `Work` on a building: completion writes BuildingSolid; deconstruction gives a half refund plus stored items.
    - `PlacePile`/`MovePile` handle refunds.
  - Cancel refunds 100%.
  - Site cells are blocked in `PathGrid` (`SetSite`: not standable, not walkable) and rebuilt in
    `SaveGame.AfterLoad`.
  - `BuildingAt` is indexed.
  - BLD-04 is enforced: an upper levee gets no deliveries until the one below is complete.
  - A blueprint clears dig marks on its ground, and `Dig` is Blocked under any footprint.
  - Building-work steps end when the building completes or is removed. The last deconstruct tick stands down if it
    would strand the worker (M4-T14) and waits while someone is on top.
- Tests: the 5 placeholders moved to `Scenarios/ConstructionScenarioTests.cs` with real bodies. There are 6 new unit
  tests in `ConstructionTests.cs` (commands and rejections, BLD-04 stacking, dig marks, save/load mid-build, codec).
  The tests did not compile before the change (missing command types).
  - Fixed two of my own new tests: the haul check waited only for the pile to vanish instead of for the delivery,
    and three `Assert.Empty(Where)` calls became `DoesNotContain` (analyzer).
  - Existing Dig tests post plain Construct jobs on cells, so construction jobs are recognised by their shape.
  - check.sh: 394 passed, 25 skipped, 0 failed.
- Review (sim-reviewer), fixes applied:
  - ADR-041 written.
  - The deconstruct wait no longer shares the counter with work ticks.
  - Fallback refund piles never land inside a building footprint.
  - Not applied (optional): a count shrunk by low stock is covered one tick later.
- Decisions: ADR-041.
- Golden: unchanged. Headless seed 1, 24,000 ticks: hash `d8a1e43aeeb5d540` (unchanged), ~23,000 ticks/s.
- Perf: perf.sh 6 passed, 2 skipped. No Godot code changed, so no screenshots.
- Next (M5-T3): levee completion must push water (WAT-12). `Complete` writes BuildingSolid via `World.SetBlock`, so
  ChangedCells should already carry it; verify in LeveeScenarioTests.
  - Deconstruct refunds land at `Construction.StandCell`.
  - Construction jobs are identified by shape (`Construction.IsSiteJob`). A future system posting 4-step Deliver
    jobs should add a marker instead.

## M5-T3 — Warehouse and levee (2026-09-26)
- Done: verification task, no sim code change needed. A completed levee writes BuildingSolid through
  `World.SetBlock` (M5-T2 `WorldActions.Complete`), so `WaterGrid.EndTick` sees it in `ChangedCells` and pushes
  the cell's water out (WAT-12) the same tick. The warehouse already works as storage: 150 total, solid goods only,
  and it gets hauls when it is nearer or the hub is full (M5-T2).
- Tests: the 2 placeholders moved to `Scenarios/LeveeScenarioTests.cs` with real bodies, plus a new warehouse test
  (3 in all). The placeholders threw before. All 3 pass on the current code.
  - Mutation check: with the BuildingSolid write in `Complete` disabled, both levee tests fail (levee cell still at
    162 water; the line lets water through). The code was then restored.
  - `LeveeInRiver_LowersDownstream` (buildings.md scenario 3): a 1-wide channel with a source at x=0 and a drain at
    x=31. Two dwarves build a levee at x=22 (rotation 90, so the entrance is downstream in wadeable water). The levee
    cell is 0 the tick it completes. The downstream cell goes 117 -> 42 in 200 ticks (the test asks for under half)
    while upstream rises 209 -> 396.
  - `LeveeLine_StopsBreachFlood` (DoD step 7 in miniature): the colonists dig a reservoir wall; the flood enters a
    2-wide tunnel; the player places a 2-levee line at x=18. The line completes at tick 333. At that point water
    has already leaked to x=24 (max 132). All of it drains within 300 ticks. Over the next 300 ticks no tunnel cell
    beyond the line holds any water, and the tunnel before the line is flooded (>= Full/2). No job failures and no
    deaths.
  - `Warehouse_TakesHubOverflow_NotWater` (BLD-10/11): a hub at 95/100 logs takes 5 of 20 piled logs and the
    warehouse takes 15. The warehouse rejects water. At 150 total it takes no more, and the pile stays (G2 answer 4).
  - check.sh: 397 passed, 23 skipped, 0 failed; Godot csproj 0 warnings.
- Decisions: none.
- Golden: unchanged. Headless seed 1, 24,000 ticks: hash `d8a1e43aeeb5d540` (unchanged), 25,360 ticks/s.
- Perf: perf.sh 6 passed, 2 skipped. No Godot code changed, so no screenshots.
- Next: M5-T4 (pump). ADR-040 allows a pump at any bank edge; water is only read at production (BLD-13).
  `WorkOnBuilding` already accepts work ticks on a Complete building (it returns Ok and does nothing), so it is the
  hook for pump cycles. Pumped water must leave the world through `WaterStats.Pumped` (WAT-11 conservation).

## M5-T4 — Pump (2026-09-26)
- Done: BLD-13/14.
  - New `Buildings/Pumps.cs` runs at ARCH-01 step 7 after `Construction`. Every tick it refreshes `NoWater` from the
    intake level (so a dry pump is flagged without a worker and clears when water returns).
  - It keeps one OperatePump job (`GoTo(entrance, Exact) -> Work(pump)`) while the buffer is under 10 and the pump
    has water, and one buffer Haul job (`GoTo(pump) -> PickUpFromStorage(pump) -> GoTo(storage) -> DeliverTo`) while
    the unpromised buffer is at least 5.
  - The cycle is `WorldActions.Work` on a complete producer (`PumpTick`): 30 ticks counted on `Building.Progress`
    (reset to 0 when a producer completes). It removes 64 units through the new `WaterGrid.Pump` (counted in
    `WaterStats.Pumped`) and adds 1 water to the buffer (`Building.Stored`), or sets NoWater. On a full buffer the
    result is `StorageFull`.
  - `JobRunner.PumpWork` cycles until the buffer is full, the pump is NoWater or it is no longer complete ("repeat
    until relieved").
  - `PickUpFromStorage` accepts a complete producer's output.
  - WAT-11 in water.md now subtracts `Pumped`; BLD-13/14 text updated.
- Tests: the 3 placeholders moved to `Scenarios/PumpScenarioTests.cs` with real bodies. They failed first (no water
  made: "condition not reached", NoWater never set). New `PumpTests.cs` (3): the cycle through `Work` (29 ticks
  nothing, tick 30 = 1 water and -64 at the intake, full buffer refuses, buffer gives out water only); deconstructing
  a worked pump stops the worker and its buffer reaches the hub as a pile; a claimed haul from a deconstructed pump
  is withdrawn.
  - `Pump_FillsHubWithWater`: 2 dwarves build the pump from 12 hub logs, then one works it and the water is hauled;
    the hub reaches 10 water, the buffer never exceeds 10, 0 failures, `Pumped == 64 x water items`.
  - `Pump_DryIntake_FlagsNoWater`: a dry bank flags NoWater, no job, dwarf idle; water at 320 at a walled intake
    gives 2 water (320 -> 256 -> 192), then NoWater again and the worker stops.
  - `Pump_RemovesWaterFromWorld`: 30-cell basin, the books balance exactly (WAT-11 with Pumped), >= 20 water made in
    ~1,500 ticks, and a save in the middle of a cycle continues with an identical hash for 300 ticks.
  - `WaterScenarioTests` river conservation formula now includes `- Pumped` (no pump there; matches the spec).
  - check.sh: 403 passed, 20 skipped, 0 failed; Godot csproj 0 warnings.
- Review (sim-reviewer): required fix applied. A buffer haul was identified by its pump still existing, so a haul
  whose pump was torn down could stay on the board forever. It is now identified by shape alone. Also applied:
  claimed hauls that have not picked up yet are cancelled when the pump goes; NoWater is false while the pump is not
  complete. Noted (not changed): an OperatePump job whose Exact entrance is unreachable fails 5 times and is posted
  again, like other kept jobs.
- Decisions: ADR-042.
- Golden: unchanged (no pumps in the golden run). Headless seed 1, 24,000 ticks: hash `d8a1e43aeeb5d540`
  (unchanged), ~23,400 ticks/s.
- Perf: perf.sh 6 passed, 2 skipped (touched water: only the new `Pump` method, not the CA step). No Godot code
  changed, so no screenshots.
- For G3 / next: ADR-040 still lets a pump go on any bank edge; on a dry one it just shows NoWater. A worked pump holds
  one dwarf until its buffer fills (priority 40, above digs/chops/hauls). Deconstruct drops the buffer as a water
  pile, which pile hauling takes to the hub.
- Next: M5-T5 (needs). Drink jobs can now take water from the hub that pumps fill. HUD NoWater icon and pump
  rendering are M5-T6.

## M5-T5 — Needs, eating, drinking, death, ColonyLost (2026-09-26)
- Done: ECO-02..07, JOB-07.
  - New `Agents/NeedsSystem.cs` runs at ARCH-01 step 6. Each tick it decays hunger by 1 and thirst by 2. While a
    need is at 0, health drops 1 per tick; otherwise it regenerates 1 on every tick divisible by 10, but not while
    Trapped. At health 0 the agent dies via `AgentSystem.Kill` (thirst wins ties).
  - Below 4000 the agent posts Drink first, then Eat (`GoToBuilding -> Consume`), through `JobRunner.AssignNeed`.
    It goes to the nearest region-reachable storage with unpromised stock and reserves the units needed to reach 9000.
  - The Consume step takes one unit per tick until the need is sated. It ends cleanly if storage runs out after at
    least one unit, and each consumed unit shrinks the reservation (`JobBoard.UseStorageOut`).
  - Retries: new agent fields `NextDrinkTick` and `NextEatTick`. A try that finds no stock, or a failed need job,
    waits 100 ticks.
  - `NeedsSystem.NoWater` / `NoFood` are derived queries for the M5-T6 HUD.
  - `AgentSystem.ColonyLost` is set, and the `ColonyLost` event emitted, once when the last agent dies.
  - Save `FormatVersion` is now 2 (the retry ticks and ColonyLost are saved and hashed).
  - `WorldFactory` gives the Great Hall its starting stock: 40 berries, 30 water, 30 logs.
  - DSG-08 deadlock fix: two diggers each standing on the other's block used to both fail after 200 ticks. Now the
    first one stands down.
  - Headless runner prints `colony.lost tick=...`.
- Tests: the 7 placeholders moved to `NeedsTests.cs` (8 tests) and `Scenarios/StarvationScenarioTests.cs` (3 tests),
  with real bodies. They did not compile first (no `NeedsSystem`, no `ColonyLost`).
  - Covered: decay, the exact threshold tick, Drink before Eat, consume counts (3 berries / 2 water, or the last unit
    only), regen, the thirst tie, the 100-tick retry plus the NoWater query, save/load mid-need, and seed-1 starting
    stock.
  - Starvation: death after exactly 1000 ticks at 0 hunger (Starved). ColonyLost fires once at the last death, and a
    save/load after it does not re-emit. A thirsty hauler drops its 8 logs, drinks, and the logs still reach the hub.
  - Existing tests changed (ADR-043):
    - `StrandScenarioTests.SmallPit...` (8,000 ticks) and `TreeFloorScenarioTests.DeepPit...` (up to 20,000 ticks)
      now stock water and berries in their hubs; otherwise the dwarves die of thirst.
    - `FloodScenarioTests.TrappedAgent_WaterRecedes_StopsDamage` now expects 972 (970 plus 2 regen ticks after the
      water drops) instead of 970.
    - DeepPit showed 3 failures (2 from the dig deadlock above, 1 path failure). After the deadlock fix it has 0.
  - check.sh: 414 passed, 13 skipped, 0 failed; Godot csproj 0 warnings.
- Review (sim-reviewer): no required fixes. Applied: the exact health value in the flood test, a simpler `WaitsOnMe`
  check, and an ADR line on need death while trapped. Not applied: moving the starting stock into data (it is a
  world-generation constant from docs/00-overview.md, not a building stat).
- Decisions: ADR-043.
- Golden: regenerated with `UPDATE_GOLDEN=1` (intentional). The hub starting stock changes the tick-0 hash, and needs
  change agent state every tick.
- Headless seed 1, no commands, 24,000 ticks: hash `854a3b12a197b9a6`, ~23,800 ticks/s. The colony is lost at tick
  15,012 (day 6): 30 water lasts three drink rounds.
  - `--script digchop`, 2400 ticks: hash `71219e5388fb912d`, 132 jobs done, 2 failed. The hub hits its 100-log cap
    (30 start + 70), so 22 logs stay in piles (G2 answer 4).
- Perf: perf.sh 6 passed, 2 skipped. No Godot code changed, so no screenshots.
- Next: M5-T6.
  - The HUD needs "No food" / "No water" from `NeedsSystem.NoFood/NoWater`, and the Colony-lost modal listens for
    the `ColonyLost` event (or reads `sim.Agents.ColonyLost` after a load).
  - `ColonistPanelModel` already labels Drinking/Eating and Starved/Died of thirst.
  - Saves from before this task (v1) no longer load (SAV-04).

## M5-T6 — Godot: build tool with ghost + reasons, building renderer, deconstruct tool, HUD top bar, Colony-lost modal (2026-09-26)
- Done: VIEW-09, 12, 14, 15, 18. No sim code changed; the view reads sim state and sends the existing
  `PlaceBuilding` / `Deconstruct` commands.
  - ViewCore (unit-tested): `Tools/BuildTool.cs` (ghost from `CanPlace`, reason text, tooltip, R rotate, B cycle,
    click / Shift-drag levee lines), `Tools/DeconstructTool.cs` (target, BLD-09 refusals, command),
    `Entities/BuildingVisuals.cs` (box, color, label, progress, NoWater), `Hud/TopBarModel.cs` (day, speed, totals,
    alerts, colony lost). `ToolKind` gains Build (B) and Deconstruct (X); only Dig/Chop/Cancel drag.
    `EntityColors` reads `buildings.*`.
  - Godot: `BuildingRenderer` (pooled boxes, labels, camera-facing progress bars, "NO WATER" billboard),
    `TopBarView` (top right), `ColonyLostModal` (Load quick save / Close), `GameRoot.Build.cs` (ghost + entrance
    tile, tooltips, clicks, Shift-drag, R/B keys, rejected-command toasts, modal on `ColonyLost` and after a load).
    The toolbar Build button is a Warehouse / Pump / Levee menu; Deconstruct is enabled.
  - Screenshot harness: `--script build` (chop + warehouse + pump + 3-levee line at the nearest valid sites) and
    `GameRoot.PickOverride`, which shows a red warehouse ghost with its tooltip on the hub roof in `build` shots.
    Also available as `run-headless.sh --script build`.
- Tests: new `View/BuildingViewTests.cs` (17 methods, 21 cases: BuildTool 8, DeconstructTool 3, BuildingVisuals 4,
  TopBar 2) and `ScreenshotScriptTests.Build_Seed1_...` (all 5 placements accepted, several states at tick 500, all
  complete by 1200). No skipped tests existed for M5-T6. Mutation check: with the ghost origin set to the picked
  cell instead of the cell in front, and red ghosts allowed to send, 4 BuildTool tests fail.
  - check.sh: 436 passed, 13 skipped, 0 failed; Godot csproj 0 warnings.
- Screenshots (Godot 4.6.2 .NET, Forward+), looked at:
  - `artifacts/screens/build500/hub.png` (`SCRIPT=build TICKS=500`): orange Great Hall box; warehouse site
    (semi-opaque, "Warehouse: log 14/20" with a green bar); levee blueprints (translucent light blue, "Levee: log 0/2");
    complete pump with a red "NO WATER" billboard; red warehouse ghost with a white entrance tile on the hub roof
    and the tooltip "Warehouse (20 log) / Needs solid ground under it"; top bar "Day 1  Paused  Log 0 ... Water 30
    Pump has no water"; toolbar shows "Build: Warehouse (B)".
  - `artifacts/screens/build1200/{hub,overview}.png`: all five buildings complete in palette colors (warehouse brown,
    pump blue, levee line grey), no labels.
  - `artifacts/screens/lost/hub.png` (`TICKS=15100`, no script): dimmed screen with the "Colony lost" modal
    ("All dwarves have died. Day 7.", Load quick save (F9), Close); colonist rows read "Died of thirst".
  - Default `artifacts/screens/{overview,river,hub,slice}.png`: unchanged apart from the hub box in its palette color
    and the top bar.
  - Known look issues: labels of neighbouring levees overlap; the hover tooltip can overlap a 3D billboard.
- Decisions: ADR-044.
- Golden: unchanged. Headless seed 1, 24,000 ticks: hash `854a3b12a197b9a6` (unchanged), ~18,700 ticks/s, colony
  lost at tick 15,012. `--script build`, 2400 ticks: hash `853ea280a7367456`, 62 jobs done, 1 failed, 84 logs stored.
- Perf: perf.sh 6 passed, 2 skipped (no sim code touched).
- Next: M5-T7 (SurvivalScript through pump + warehouse + levee).
  - **On seed 1 no valid pump site has water at its intake** (1,921 valid sites, 0 wet): the banks rise one level
    per cell, so a pump whose front overhangs the river has its entrance inside the next bank step
    (`EntranceBlocked`). Digging that one entrance cell fixes it. Checked at x=39: after `(39,18,79)` is dug, a pump at
    `(39,18,80)` rotation 0 is Ok with its intake at 1024. The script must dig that notch first, then place the pump
    after the dig is done. Without a working pump the colony dies of thirst on day 6.
  - The ghost check uses `CanPlace` on current state, so a Shift-drag across cells whose blueprints are not applied
    yet can still be rejected by the sim; the rejection is toasted.

## M5-T7 — Extend SurvivalScript through pump + warehouse + levee; regenerate golden (2026-09-26)
- Done: new `Aurvangar.ViewCore.Scripts.SurvivalScript` (ADR-045): a fixed `(tick, command)` list for seed 1 with
  `EnqueueDue(sim)` (call before each `Tick()`) and `Run(sim, ticks)`. No sim code changed.
  - Tick 0: dig the pump-entrance notch `(40,18,79)` and chop the 48x48 area around the Great Hall. Tick 600: pump at
    `(40,18,80)` rotation 0 (intake 1024; notch dug at tick 485). Tick 1200: warehouse at `(34,24,54)`. Tick 1800:
    five levees `(33..37,23,76)` rotation 0 along the top of the river bank.
  - The M5-T6 notch `(39,18,79)` does not work: a berry bush stands on it, so its dig never posts (DSG-03). x=40 is
    the nearest wet pump site whose one-cell notch has no plant.
  - `GoldenHashTests` now runs the script. `run-headless.sh --script survival` enqueues the due commands each tick.
    docs/testing.md "Scripted play" updated.
- Tests: new `Scenarios/SurvivalScriptTests.cs` (3): commands in tick order and logged at exactly their ticks; all
  9 commands accepted, pump + warehouse + 5 levees complete within day 1, pump wet, hub water > 50 at tick 3000,
  0 failed jobs; all 5 alive at day 7 with >= 90 hub water, and through day 10 no one Dehydrated and no ColonyLost.
  The script was written before these tests (to find valid coordinates), so they passed on first run; the golden
  test failed first for the right reason (hashes at 1200/3000/6000 changed once the script was applied).
  - check.sh: 439 passed, 13 skipped, 0 failed; Godot csproj 0 warnings.
- Decisions: ADR-045.
- Golden: regenerated with `UPDATE_GOLDEN=1` (intentional): the golden run now applies the survival script (before
  it ran with no commands). Tick 0 unchanged (`057b999ab23c4a64`); 1200 `417f878bba53e2e5`, 3000 `6c554c5ab61035ce`,
  6000 `25531e45ab061864`.
- Headless seed 1, `--script survival`, 24,000 ticks: hash `48a680f2f8ea3bab`, ~14,500 ticks/s, 154 jobs done,
  0 failed, 23 trees felled, 1 cell dug, stored log 80 / water 110 (hub 100 + pump buffer 10), 0 trapped. Nobody dies
  of thirst; the first dwarf starves at tick 23,091 (4 alive at day 10), colony lost at 29,011 when the 40 berries
  are long gone. No-script run unchanged: `854a3b12a197b9a6`, colony lost 15,012.
- Perf: perf.sh 6 passed, 2 skipped (no sim code touched). No rendering code changed, so no screenshots.
- Next: M6-T1. M6-T6 must add food (farm field near the river, berry harvest) to the script so all 5 live to day
  10; append its commands to `SurvivalScript.Build()` after tick 1800 and keep `EnqueueDue` semantics. The screenshot
  harness does not support `SCRIPT=survival` yet (it enqueues all commands before tick 1); M6-T8 may want it.

## M6-T1 — Moisture map (2026-09-26)
- Done: ECO-15, ECO-16. New `Water/MoistureMap.cs` (`sim.Moisture`), ticked at ARCH-01 step 4: it recomputes when
  `tick % 50 == 0`, after the water step.
  - A column's surface is its highest solid cell. The column is moist when water >= 128 lies within Chebyshev
    radius 5 at `y in [surfaceY - 2, surfaceY + 1]`; a column with no solid cell is dry.
  - Algorithm: one top-down layer pass builds per-column height bitmasks of wet cells (skipping groups of four empty
    cells) and the surface heights; then a separable OR dilation (x, then z) and a window test per column.
  - API: `IsMoist(x, z)`, `Flags` (byte per column), `SurfaceY(x, z)` (reads the current world), `Recompute()`.
    `VoxelWorld.IsSolidBlock(byte)` added.
  - The flags are hashed and saved (new `Moisture` section after plants, `FormatVersion` 3), not recomputed on load:
    between recomputes they reflect older water, so a recompute on load would break SAV-03 once crops read them
    (ADR-046; SAV-01 updated).
- Tests: the 3 placeholders moved to `MoistureTests.cs` with bodies (13 cases): radius 5 moist / 6 dry, including
  diagonals and an exact 11x11 count; level 127 vs 128; height window (water at surface + 2 / + 1 / 0 / - 2 / - 3);
  a column with no solid; recompute only at ticks 0, 50, 100; a save at tick 25 keeps the stale map and the hash and
  both copies turn moist at tick 50; the map is hashed; seed-1 banks moist, Great Hall dry. `OtherPerfTests.
  Moisture_Recompute` has a body and is un-skipped. The tests did not compile first (no `Simulation.Moisture`).
  - check.sh: 452 passed, 10 skipped, 0 failed; Godot csproj 0 warnings.
- Review (sim-reviewer): no rule violations; agreed with saving the map. It reported ECO-16 as failing at 3.5-3.9 ms,
  but that was a Debug build; perf.sh (Release) measures 0.99 ms. Applied: a comment that `SaveSection` enum order is
  not the on-disk order, and ADR-032's recompute-on-load line is marked superseded. Noted for M6-T2: `SurfaceY` reads
  the live world while the flags reflect the last recompute.
- Decisions: ADR-046.
- Golden: regenerated with `UPDATE_GOLDEN=1` (intentional): `StateHash` now includes the moisture flags. Nothing reads
  them yet, so behavior is unchanged. New: 0 `79abf70fc6d591a0`, 1200 `28f147c5e5ed96de`, 3000 `dca29ab9252d4323`,
  6000 `b02a6ce078f54935`.
- Perf: perf.sh 7 passed, 1 skipped (SIM-P1, M6-T7). ECO-16 moisture recompute median 0.99 ms, p95 1.04 ms (budget
  3 ms). WAT-P1 1.71 / 1.13 ms, WAT-P2 699 active, PTH-P1 p95 0.77 ms, PTH-P2 warm 4.67 ms, MESH-P1 0.62 ms. One
  earlier perf.sh run had PTH-P1 p95 at 1.519 ms, with every timing about 2x slower (machine load); three reruns gave
  0.75-0.83 ms and a full rerun was green. No path code changed.
- Headless seed 1, `--script survival`, 24,000 ticks: hash `3c4a16db6749c7b8`, ~12,200 ticks/s, 154 jobs done,
  0 failed (same stats as M5-T7). No script: hash `5bb7582c41e4551b`, colony lost at tick 15,012 (unchanged).
- Next: M6-T2. Crops read `sim.Moisture.IsMoist(x, z)` for their column; ECO-11's farm tile is the column's top
  surface cell, so `SurfaceY` should match the tile's y. Farm tiles need their own save section (bump FormatVersion
  to 4) and hash. No view event exists for moisture changes yet; M6-T5 can add one if it renders moisture.

## M6-T2 — Farm designation and crops (2026-09-26)
- Done: ECO-11..14, JOB-11 for crops, DSG-06 for farm tiles.
  - New `Farming/FarmSystem.cs` (`sim.Farms`). It ticks at ARCH-01 step 5, after the plants, and does three things
    in order. It drops lost tiles (the block is no longer Farmland, or the cell above is solid or part of a
    building). It grows crops from `sim.Moisture.IsMoist`: while moist, Progress +1 and DryTicks reset; Mature at
    7200. While dry, DryTicks +1; the crop withers to Empty at 2400. It then keeps one Plant job (30, Work 30) per
    Empty tile and one Harvest job (35, Work 20) per Mature tile, and withdraws unclaimed stale ones.
  - Command `DesignateFarm(X0, Z0, X1, Z1)` covers an XZ rectangle. In each column the top solid cell qualifies if
    it is Grass, Dirt, or Farmland without a tile, has standable air above, and is not part of a building. It
    becomes Farmland at once. The command is in `CommandCodec`.
  - New actions `WorldActions.Plant` / `Harvest` (step kinds `Plant`, `Harvest`). Harvest puts 3 potatoes in the
    carried stack.
  - JOB-11: after the harvest, the job appends GoToBuilding + DeliverTo to the nearest storage with room for all 3
    (`HaulSystem.NearestStorage`). It swaps the tile's cell reservation for that storage reservation
    (`JobBoard.SetReservations`). If no storage has room, it appends a Drop and the pile is hauled later.
  - `CancelDesignation` removes Empty tiles (Farmland stays) and cancels their Plant jobs.
  - Tiles are saved (new `Farms` section; `FormatVersion` is now 4) and hashed.
- Tests: the 4 placeholders moved to `Scenarios/FarmScenarioTests.cs` with bodies, plus 2 new tests
  (`ShortDrySpell_DoesNotWither`, `Harvest_StorageFull_DropsPile`). New `FarmTests.cs` has 7 tests: designation
  rules, rejection, re-designating Farmland, cancel, tile loss on dig/cover, save/load mid-growth, hash coverage.
  - The tests did not compile first (there was no `FarmSystem` or `DesignateFarm` yet).
  - Mutation check: with growth ignoring moisture and no storage chaining, 4 scenario tests fail.
  - check.sh: 465 passed, 6 skipped, 0 failed; Godot csproj 0 warnings.
- Review (sim-reviewer): no rule violations. Its required fixes were ADR-047, which I had not yet written when it
  read the diff, and the golden note below. Applied: job selection skips a Plant / Harvest job whose tile no longer
  needs it (`FarmSystem.StillWanted`). This covers a harvest job released mid-delivery by a need job, which could be
  re-taken in the same tick. Not applied (noted): `NearestStorage` has no region check (same as `HaulSystem.Plan`);
  per-tick HashSets in `SyncJobs` (see M6-T7).
- Decisions: ADR-047. ECO-11 and JOB-11 are annotated in the specs.
- Golden: regenerated with `UPDATE_GOLDEN=1` (intentional). `StateHash` now includes the farm table (its count is 0
  on the survival script), so every checkpoint changed, including tick 0. Behavior is unchanged. New hashes: 0
  `3f06893d65057740`, 1200 `3766ea79b5623c8e`, 3000 `289e98388be50fc3`, 6000 `712f3f4bbf421a95`.
- Headless seed 1, `--script survival`, 24,000 ticks: hash `7512509798ef6638`, ~12,100 ticks/s. Stats are the same
  as M6-T1 (154 jobs done, 0 failed; 4 alive at day 10). No script: hash `8ea72c37311ecf3b`, colony lost at 15,012.
- Perf: perf.sh 7 passed, 1 skipped (SIM-P1). No Godot code changed, so no screenshots.
- Next: M6-T3 (berry bushes, ECO-10: Harvest from PlantSystem while stored food < 60).
  - `FarmSystem.ChainDelivery` / `HaulSystem.NearestStorage` can be reused for the bush harvest's JOB-11 chain.
  - `FarmSystem.StillWanted` only covers Plant and Harvest jobs that target farm tiles. Bush harvest jobs also use
    `JobKind.Harvest`, so tell them apart, for example by the tile lookup or a different step.
  - M6-T5 renders crops from `sim.Farms.All` (state and Progress / `MatureTicks`). There is no farm view event.
  - M6-T6 adds `DesignateFarm` near the river to the SurvivalScript. A tile must be within 5 columns of water at
    [surfaceY - 2, surfaceY + 1] to grow.

## M6-T3 — Berry bushes (2026-09-26)
- Done: ECO-10. `PlantSystem.Tick(sim)` (ARCH-01 step 5) counts a harvested bush down from 1200 and makes it ripe
  (2 berries) exactly 1200 ticks after the harvest tick. New `Plants/BushHarvest`:
  - `Sync` runs at the end of the plant step. While food in storage is below 60, it keeps one Harvest job (35) per ripe
    bush: `GoTo(reach) → Work(20) → HarvestBush(plant id)`, with a cell reservation on the bush.
  - Food in storage = units of food items (berries + potatoes) in complete storage buildings (ADR-048).
  - It withdraws unclaimed bush jobs when food is at least 60, when the bush is not ripe, or when the job already has
    delivery steps (it was released mid-delivery). Job selection skips such jobs (`BushHarvest.StillWanted`).
  - Bush jobs share `JobKind.Harvest` with crops. They are told apart by their third step, `HarvestBush`
    (`BushHarvest.Is`), and FarmSystem ignores them.
  - New `WorldActions.HarvestBush`. The berries go to storage in the same job through `FarmSystem.ChainDelivery`
    (JOB-11).
- Fix (BLD-10, latent since M5-T2): a successful `PickUpFromStorage` step now uses up its job's StorageOut reservation.
  - Before, a Deliver job kept that promise until it finished, so a second job reserving the building's remaining logs
    failed its pickup. That showed up as one failed levee Deliver once berry picking shifted the timings.
  - Review follow-up: a Deliver or pump-buffer Haul job released after its pickup is re-planned when its reservations
    differ from the posted ones, so it gets its StorageOut back.
- Knock-on changes (ADR-048): seed 1 starts with 40 food, below 60, so from tick 0 all five dwarves pick berries for
  about 500 ticks (Harvest 35 is above Dig/Chop 25).
  - `SurvivalScript` now chops at tick 600, together with the pump, instead of tick 0. At tick 0 the chops tied with the
    notch dig and held it until tick 1136, so the pump was rejected. The notch is now dug at tick 416.
  - `AgentMovementTests.Seed1_FiveColonistsSpawnStandable` checks the Idle/NextCell spawn state before the first tick.
  - `ScreenshotScriptTests` build: everything is complete by tick 1600 (was 1200). Docs in testing.md and
    screenshot.sh are updated: `SCRIPT=build` needs `TICKS=1600` to show the buildings complete.
- Tests: the 2 placeholders moved to `BushTests.cs` with bodies, plus 4 new tests (withdraw at 60 / repost below it,
  action rules, save/load mid-regrowth, hash). They did not compile first (no `BushHarvest` or `HarvestBush` yet).
  - New regression tests: `JobBoardTests.PickUpFromStorage_UsesUpItsStorageOutReservation` and
    `ConstructionTests.DeliverReleasedAfterPickup_GetsItsReservationBack`. Each was mutation-checked: it fails without
    its fix.
  - check.sh: 473 passed, 4 skipped, 0 failed; Godot csproj 0 warnings.
- Review (sim-reviewer): no hard-rule violations. Its one required fix, re-planning released Deliver / buffer Haul
  jobs, is applied. Not applied (optional): computing food once per tick; the Sync scan over all plants (M6-T7 perf
  pass); an assert for a hand-built bush with 0 berries and 0 regrow.
- Decisions: ADR-048. ECO-10 is annotated.
- Golden: regenerated with `UPDATE_GOLDEN=1` (intentional). Berry picking now starts at tick 0 and the script's chop
  moved to tick 600. New hashes: 0 `2dce28cc9774b9be`, 1200 `278032913bfc4cea`, 3000 `ea48ace22f08bf21`, 6000
  `9f3182b7787a6769`.
- Headless seed 1, `--script survival`, 24,000 ticks: hash `6492794254d882eb`, about 11,500 ticks/s, 196 jobs done,
  0 failed, stored log 80 / berries 71 / water 110, all 5 alive at day 10 (before: 4). No script: hash
  `cc217e10d620371d`, colony lost at 15,009 (thirst).
- Perf: perf.sh 7 passed, 1 skipped (SIM-P1). One earlier run, made while the reviewer agent was building, failed
  PTH-P1 at p95 1.555 ms (known noise); the rerun passed. No path or water code changed.
- Screenshots: none. No rendering code changed; the ViewCore changes are the script and doc comments.
- Next: M6-T4 (weather). For M6-T6:
  - The survival script already keeps all 5 alive to day 10 on berries plus the pump.
  - `Seed1_NoCommands_ColonyLost` still holds: the colony dies of thirst at 15,009.
  - Any new script command near tick 0 competes with berry picking for about 500 ticks.
  - The M6-T5 HUD can show `BushHarvest.FoodInStorage`. Bush ripeness is `Plant.Berries` / `RegrowTicks`.

## M6-T4 — Weather and drought (2026-09-26)
- Done: ECO-17, WAT-09; the ECO-18 readout API (the HUD itself is M6-T5).
  - New static `Water/WeatherSystem` at ARCH-01 step 2. The season is a pure function of the tick
    (`tick % 7 days < 5 days` → Wet, else Drought), so there is no new state. The strength it sets is
    `WaterGrid.SourceStrength`, which was already saved and hashed. No FormatVersion bump.
  - The strength is written only on the first tick of a season (5 d, 7 d, 12 d, ...; tick 0 keeps the default 100).
    A new `SeasonChanged(Season)` event fires then. A strength set by hand stays until the next season change.
  - `SeasonAt(tick)`, `TicksUntilChange(tick)` and `DaysUntilChange(tick)` (rounded up) are for the M6-T5 HUD.
  - WAT-09 is now literal: a source is set to the target both ways, and lowering counts as `Drained` (ADR-049,
    amends ADR-011). With raise-only springs, the drought left 79% of the river after 2 days. Now the spring columns
    act as sinks in a drought.
- Seed-1 river (no commands): 99% at day 5 → 23% after 200 drought ticks → empty by ~800. It is back at 96% 400 ticks
  after the drought and at 99% by 1,000.
- Tests: un-skipped `RiverTests.DroughtDrainsRiver`. New `WeatherTests.cs` (15): schedule and days-left table,
  strength follows the season, `SeasonChanged` only on transitions, drought source seeps with WAT-11 conservation,
  manual strength kept until the next change, save/load mid-drought matches a continuous run across the change to Wet.
  With the weather step disabled, the 4 system tests and `DroughtDrainsRiver` failed. With raise-only sources,
  `DroughtDrainsRiver` still failed.
  - `Seed1_RiverHoldsVolumeThroughDay10` (M3-T8) changed: it now checks ±10% in the Wet season only, skipping the
    600-tick refill after a drought (186 samples, count asserted). The old form cannot hold with a working drought
    (it failed at tick 13,700, 89%). Recorded in ADR-049.
  - check.sh: 489 passed, 3 skipped, 0 failed; Godot csproj 0 warnings.
- Decisions: ADR-049. ECO-17 and WAT-09 are annotated.
- Golden: unchanged. All golden checkpoints (0 / 1200 / 3000 / 6000) are before the first drought, and nothing changes
  at strength 100.
- Headless seed 1, `--script survival`, 24,000 ticks: hash `c24f24f134a69709`, all 5 alive at day 10, 190 jobs done,
  0 failed, stored log 80 / berries 67 / water 110. The 110 stored water carries the colony through the dry pump.
  4,432 ticks/s (median tick 0.063 ms, p95 1.0 ms), down from ~9,000 ticks/s before day 5. During drain and refill,
  active water cells peak near 3,500, and regions rebuild often (903 rebuilds vs 139 by day 5). No script: hash
  `6cef6aa7d84e85dd`, colony lost at 15,009 (thirst, unchanged).
- Perf: perf.sh 7 passed, 1 skipped (SIM-P1). WAT-P1 1.73 / 1.12 ms, WAT-P2 699 active, ECO-16 0.91 ms, PTH-P2 warm
  3.88 ms, MESH-P1 0.62 ms. PTH-P1 p95 was 1.19 ms on the first run (noise, budget 1.5 ms); two reruns gave 0.77-0.79 ms.
  No path code changed.
- Screenshots: none. No rendering or Godot code changed.
- Next: M6-T5. The season HUD reads `WeatherSystem.SeasonAt(sim.Clock.Tick)` / `DaysUntilChange`, and can refresh on
  `SeasonChanged`. The river visibly empties during days 5-7 (WaterDirty fires normally).
  - M6-T6: farm crops wither during every drought unless they stay moist.
  - M6-T7: the region rebuild churn and active-cell peak during drain/refill are the main new tick cost. SIM-P1 is
    measured at day 5, the first drought tick.

## M6-T5 — Godot: farm tool, crop rendering, season HUD (2026-09-26)
- Done: view side of M6 (ADR-050). No sim code changed.
  - Farm tool (F, toolbar button now enabled): drag an XZ rectangle, sends `DesignateFarm`; green preview box. Its mouse
    label says whether the hovered column is moist, or "m of n columns moist" during a drag (`ViewCore/Tools/FarmTool`).
  - Farm tiles: two raised furrows each in `designations.farm` (VIEW-11 overlay, in `DesignationMesher`).
  - Crops (`ViewCore/Entities/CropMesher` + new `CropRenderer`): 2×2 stalks that grow in 8 stages; green when moist,
    straw (`plants.cropDry`, new palette key with `plants.crop`) when dry; potato caps when mature. Polled by a
    signature (no farm event). Hover label on a tile gives the state, percent, moisture and ticks until it withers.
  - Ripe bushes show 4 red berry cubes; `PlantRenderer` rebuilds on a signature that includes ripeness.
  - Top bar: "Wet season, N days left" / "Drought, N days left" (orange), toast on `SeasonChanged`. The bar is now
    anchored to the top right.
  - Screenshot harness: `SCRIPT=farm` (5×5 field on the nearest moist ground to the hub, found on a private
    `MoistureMap`, read only) and an opt-in `farm` preset (`SHOTS=farm`). Default shots unchanged
    (`ScreenshotPresets.DefaultShots`). `run-headless.sh --script farm` also works.
- Tests: new `View/FarmViewTests.cs` (18 incl. theory rows): tool hotkey/drag/command, moisture tooltip, crop
  stages/colors/slice/signature/label, farm overlay, bush berries + signature, season text for 5 ticks across the
  cycle, season toast, farm script + preset. They did not compile first (no Farm tool, CropMesher, FarmTool, season
  fields). Updated (intended look change): `PlantMesherTests.Bush_SmallConeWithinItsCell` and
  `SeedOne_AllPlantsMeshed` now count the berry cubes of ripe bushes; `ScreenshotArgsTests.Defaults_WhenNoArgs`
  keeps passing because the defaults are now `DefaultShots`, not all preset names.
  - check.sh: 507 passed, 3 skipped, 0 failed; Godot csproj 0 warnings.
- Decisions: ADR-050. VIEW-11 and VIEW-15 annotated.
- Golden: unchanged (no sim change).
- Headless seed 1, `--script farm`, 9,000 ticks: hash `f8c960dcc30fcceb`, ~8,800 ticks/s, stored potato 75,
  berries 77 (25 tiles, first harvests before day 4).
- Perf: perf.sh 7 passed, 1 skipped (SIM-P1). The first run failed PTH-P1 at p95 1.517 ms (known noise, budget
  1.5 ms; no path code changed); the rerun passed.
- Screenshots (Forward+, looked at each): `artifacts/screens/farm4000/{farm,hub}.png` (young crops, berries on
  bushes, "Wet season, 4 days left"), `farm7700/farm.png` (tall green crops, first potato caps, close-up at distance
  14), `farm9000/{farm,river}.png` (all harvested, 75 potatoes, replanting on bare furrows),
  `farm13200/farm.png` (drought: orange "Drought, 2 days left", toast, river bed empty, crops straw colored).
  - Harness quirk: `screenshot.sh`'s `--build-solutions` once did not pick up a ViewCore change (stale
    `.godot/mono/temp/bin/Debug/Aurvangar.ViewCore.dll`). Run `dotnet build src/Aurvangar.Godot` (check.sh does it)
    before `screenshot.sh` after editing ViewCore.
- Next: M6-T6 (complete SurvivalScript). The farm script shows a 5×5 field at the nearest moist site to the hub; its
  corner is `ScreenshotScripts.FindFarm(sim)` on seed 1 if the survival script wants the same spot. Crops there
  are straw colored and wither during every drought (river empty days 5-7); the 9,000-tick run already stores 75
  potatoes before the first drought.

## M6-T6 — Complete SurvivalScript (farms, hill dig, breach, levee repair); survival scenario: all 5 alive at day 10 on seed 1 (2026-09-26)
- Done: `SurvivalScript` (ViewCore) now has the whole DoD command log (ADR-051). No sim code changed.
  - Tick 2400: 6×6 farm `(62,67)..(67,72)` (nearest all-moist field, the `FindFarm` search with size 6).
  - Tick 3000: two-wide tunnel `(84,17,56)..(85,18,65)` into the hill's south slope, floor one below the river surface,
    entered from the bank top; 22 stone dug and hauled by ~6,900.
  - Tick 7200 (breach): dig the bank cells `(84..85,17,66)`; the river floods the tunnel to deep water; a dwarf in it
    flees out. Tick 7500 (repair): a levee on each breach cell (entrance in the flooded tunnel, built from the dry bank
    top); both complete by 7,906. The tunnel keeps its water through the drought while the river outside is dry.
  - docs/testing.md "Scripted play" now lists the script as a table.
- Tests: the two placeholders moved from `PendingAcceptanceTests.cs` (file removed, nothing else in it) to
  `Scenarios/SurvivalScenarioTests.cs` with bodies:
  - `Seed1_SurvivalScript_AllAliveAtDay10`: 36 farm tiles, tunnel dug and ≥ 10 stone stored before the breach, tunnel
    deep after it, both repair levees complete and potatoes stored by day 5, tunnel still deep mid-drought with the
    river beside the breach at 0, no rejected commands, no deaths, all 5 alive at day 10, at least one dwarf caught in
    the flood lived.
  - `Seed1_NoCommands_ColonyLost`: without commands the colony is lost before day 7, all dead, one Dehydrated.
  - The tests reference the new script members, so they did not compile against the M5-T7 script. Mutation check:
    without the repair levees the scenario test fails (repair count).
  - check.sh: 509 passed, 1 skipped (`SaveSize_Day5_Under3MB`, M6-T7), 0 failed; Godot csproj 0 warnings.
- Decisions: ADR-051.
- Golden: regenerated with `UPDATE_GOLDEN=1` (intentional): the farm at tick 2400 is new script content. 0 and 1200
  unchanged; 3000 `25f61178eeb2b39f`, 6000 `01b5005c23875f6c`.
- Headless seed 1, `--script survival`, 24,000 ticks: hash `7344b913cc2909c9`, 3,713 ticks/s (median 0.068 ms, p95
  1.18 ms), all 5 alive, 406 jobs done, 0 failed, 978 region rebuilds, stored log 76 / stone 22 / berries 39 /
  potato 105 / water 110, 0 trapped.
- Perf: perf.sh 7 passed, 1 skipped (SIM-P1 placeholder). No water or path code changed.
- Screenshots: none. No rendering or Godot code changed; the screenshot harness still does not run `SCRIPT=survival`.
- Next: M6-T7 (perf pass). SIM-P1 runs the survival script to day 5 (tick 12,000); the tunnel flood and its levees now
  exist then. M6-T8: the headless `summary:` dig/chop counts are measured against the marks after tick 1, so with
  `--script survival` they report only the tick-0 notch (1 cell dug, 0 trees); use the stored stone (22) and the tests
  as evidence, or fix the summary for timed scripts.

## M6-T7 — Full perf pass (2026-09-26)
- Done: the last two skipped tests have bodies, and every budget is green with room to spare (ADR-052).
  - SIM-P1 `OtherPerfTests.FullTick_Seed1_Day5`: seed 1 + `SurvivalScript` to tick 12,000 (first drought tick), then
    times 500 `Tick()` calls (script enqueue and event drain outside the timer). Prints the water and region phase
    totals from a test `ITickProfiler` (`PhaseTimer`).
  - SAV-05 `SaveLoadTests.SaveSize_Day5_Under3MB`: same run, save ≤ 3 MB, and it loads back to the same hash.
  - PTH-P1 kept failing now and then in `perf.sh` (p95 1.50-1.60 ms vs ~0.8 ms alone). The perf-auditor traced this
    to Windows parking the windowless testhost on E-cores (EcoQoS) on this i9-12900KF, made worse by perf classes
    running in parallel. Fixes: (1) harness: one non-parallel `Perf` collection; `PerfHelpers.SettleGc()` (full GC,
    plus a Windows-only opt-out of power throttling) before timed sections. (2) A*: packed `ulong` heap key
    (f/h/index = 20/16/28 bits, same order), one `Node[]` for per-cell search state, direct heuristic when there is
    one goal. `EnsureArrays` throws if a world would overflow the key. Paths are identical: the survival hash is
    unchanged, and so are the goldens. The auditor also hashed 20,000 random FindPath/FindFlee results and got the
    same hash before and after.
- Tests: un-skipped/implemented `FullTick_Seed1_Day5` and `SaveSize_Day5_Under3MB`. They passed on first run: the
  code was already within budget, so there was no failing implementation to fix. check.sh: 510 passed,
  0 skipped, 0 failed; Godot csproj 0 warnings.
- Decisions: ADR-052.
- Golden: unchanged.
- Perf (perf.sh, Release, 9 of 9 incl. SAV-05; 6 back-to-back suite runs all green):

  | ID | Budget | Measured |
  |---|---|---|
  | WAT-P1 | ≤ 4 ms @ 20k | 1.28 ms median (23,048 active); 0.86 ms at 128×128 |
  | WAT-P2 | ≤ 3,000 active | 699 |
  | PTH-P1 | p95 ≤ 1.5 ms | p95 0.60-0.62 ms (median 0.24), was 0.8-1.6 |
  | PTH-P2 | ≤ 25 ms | warm 2.1 / cold 2.4 ms (up to 4.2 / 6.2 in some runs) |
  | ECO-16 | ≤ 3 ms | 0.87 ms |
  | SIM-P1 | median ≤ 8 ms | 2.34 ms median, p95 2.57, max 3.6 |
  | MESH-P1 | ≤ 6 ms | 0.65 ms |
  | SAV-05 | ≤ 3 MB | 31,781 bytes |

  SIM-P1 breakdown over ticks 12,000-12,499: region rebuilds are ~77% of the tick (407 full rebuilds in 500 ticks,
  about 2.2 ms each), because the draining river changes walkability every tick. Water is 0.12 ms/tick; everything
  else (moisture, plants, farms, needs, buildings, designations, haul, agents) adds up to under 0.1 ms/tick. Before the
  QoS fix, the same test measured 4.3-4.9 ms median whenever it landed on E-cores.
- Headless seed 1, `--script survival`, 24,000 ticks: hash `7344b913cc2909c9` (same as M6-T6), 3,783 ticks/s, all
  5 alive, 0 failed jobs, 978 region rebuilds.
- Screenshots: none (no rendering or Godot code changed).
- Next: M6-T8 (DoD walkthrough). If the tick cost ever matters, the next lever is incremental region updates, or
  skipping rebuilds when the changed cells do not change connectivity, during river drain and refill. The headless
  `summary:` dig/chop counts still cover only the tick-0 marks for timed scripts (see the M6-T6 note).

## M6-T8 — Definition-of-done walkthrough, headless (2026-09-26)
- Done: every DoD step (docs/00-overview.md) mapped to evidence below (ADR-053). No sim code changed.
  - Headless runner: dig/chop/crop counts are now cumulative (observed after every tick, outside the timed section),
    so `--script survival` reports the real work (43 cells dug, 23 trees felled; was 1 / 0). New `colony:` line per
    report (season, complete buildings, pump NoWater, crop states, storage by item) and summary rows for crops
    harvested/withered and pump NoWater ticks by season. Hash unchanged.
  - Two new evidence tests for the parts of DoD 8 and 10 that nothing covered yet (see checklist).
- Tests: new `Scenarios/ReservoirScenarioTests` (2): `LeveeReservoir_PumpRunsThroughDrought` and its control
  `OpenPool_PumpRunsDryInDrought` (same world without the levees: the pool drains and the pump flags NoWater, which
  is what makes the first test fail without levees). New
  `SurvivalScenarioTests.Seed1_SurvivalScript_SaveLoadMidSession_ContinuesIdentically`. All three passed on their
  first run (the behavior already existed; these are evidence, not new features). The control's first draft also
  asserted the river bed at the mouth was 0 mid-drought; the open pool is still draining into it then (level 154), so
  that check was dropped from the control only.
  - check.sh: 513 passed, 0 skipped, 0 failed; Godot csproj 0 warnings.
- Decisions: ADR-053.
- Golden: unchanged.
- Perf (perf.sh, Release): 8 passed. WAT-P1 1.28 ms median (23,048 active) / 0.85 ms at 128×128; WAT-P2 699 active;
  PTH-P1 p95 0.64 ms; PTH-P2 warm 2.20 / cold 2.53 ms; ECO-16 0.88 ms; MESH-P1 0.64 ms; SIM-P1 median 2.46 ms
  (p95 2.64, max 3.72; regions 943 ms of 500 ticks, 407 rebuilds). SAV-05 (in check.sh): day-5 save under 3 MB.

### Headless run: `./scripts/run-headless.sh --seed 1 --script survival --ticks 24000 --report-every 2400`
Hash `7344b913cc2909c9` (same as M6-T6/T7), 3,670 ticks/s, tick median 0.077 ms, p95 1.18 ms.

| Day | Season | Complete buildings | Pump NoWater | Farm (growing/mature) | Storage | Water volume |
|---|---|---|---|---|---|---|
| 1 | Wet | hub, pump, warehouse, 5 levees | 0 | – | log 72, berries 68, water 56 | 4,905,333 |
| 2 | Wet | same | 0 | 36 tiles (35/0) | log 80, stone 8, berries 68, water 84 | 4,905,352 |
| 3 | Wet | same | 0 | 35/0 | log 80, stone 22, berries 69, water 110 | 4,905,361 |
| 4 | Wet | + 2 breach levees (7) | 0 | 35/0 | log 76, stone 22, berries 69, water 105 | 4,919,267 |
| 5 | Drought | 7 levees | 0 | 35/0 (harvest done) | + potato 105, water 110 | 4,919,320 |
| 6 | Drought | 7 levees | 1 | 36/0 (replanted, drying) | berries 54, water 100 | 15,125 |
| 7 | Wet | 7 levees | 1 | 36/0 | berries 54, water 90 | 15,125 |
| 8 | Wet | 7 levees | 0 | 36/0 | berries 39, water 110 | 4,914,894 |
| 10 | Wet | 7 levees | 0 | 24/12 | log 76, stone 22, berries 39, potato 105, water 110 | 4,914,948 |

Summary: 5/5 alive, 406 jobs done, 0 failed, 43 of 43 marked cells dug, 23 of 23 marked trees felled, 0 items on the
ground, 35 crops harvested (105 potatoes), 41 crops withered (all in or right after the drought), pump NoWater on
5,004 ticks (4,712 in the drought, 292 while the river refilled), 568 path searches, 978 region rebuilds, 0 trapped.
During the drought the whole river drains: 15,125 water units stay in the world, which is the walled-off tunnel.

### DoD checklist (seed 1)
| # | DoD step | Evidence |
|---|---|---|
| 1 | Map loads: 128×128×64, hill, river W→E, trees, bushes, Great Hall on a flat by the river, 5 dwarves, 40 berries / 30 water / 30 logs | Headless: size 128x64x128, 150 trees, 24 bushes, 1 building, 5 agents. Tests: `TerrainGeneratorTests.{Heights_WithinSpecRange, River_CrossesMap_WithSourcesAndDrains, Plants_CountsAndSpacing, HubOnSpawnFlat_BushesNearRiverAndHub}`, `RiverTests.Seed1_RiverFlowsAcrossMap`, `WorldInvariantTests.*`, `NeedsTests.Seed1_HubStartingStock`. Screenshots `artifacts/screens/{overview,river,hub}.png` (G1/G2). |
| 2 | Pump on the bank; built; fills storage with water | Headless: pump complete by day 1, stored water 56 → 110 by day 3. Tests: `SurvivalScriptTests.Seed1_BuildsPumpWarehouseAndLevees_AllAccepted`, `SurvivalScriptTests.Seed1_PumpOutlastsStartingWater_NoOneDiesOfThirst`, `PumpScenarioTests.Pump_FillsHubWithWater`. Screenshots `build1200/`. |
| 3 | Farm field near the river; planted; potatoes grow only on moist tiles | Headless: 36 tiles on day 2, 35 harvested before the drought (potato 105). Tests: `FarmScenarioTests.{MoistTile_MaturesAt7200, DryTile_NeverGrows, Harvest_Yields3Potatoes}`, `MoistureTests.Seed1_RiverBanksMoist`, `SurvivalScenarioTests.Seed1_SurvivalScript_AllAliveAtDay10` (36 tiles, potatoes by day 5). Screenshots `farm4000/`, `farm7700/`, `farm9000/`. |
| 4 | Chop; trees felled; logs hauled to storage | Headless: 23 of 23 marked trees felled, 0 items left on the ground, logs 72-80 stored. Tests: `DigScenarioTests.Chop_MarkedTrees_LogsHauled`, `TreeFloorScenarioTests.*`. Screenshot `digchop/hub.png`. |
| 5 | Warehouse placed and built | Headless: warehouse complete by day 1. Tests: `ConstructionScenarioTests.Warehouse_BuiltByTwoAgents`, `SurvivalScriptTests.Seed1_BuildsPumpWarehouseAndLevees_AllAccepted`, `LeveeScenarioTests.Warehouse_TakesHubOverflow_NotWater`. |
| 6 | Dig into the hill to stone; dig and haul stone | Headless: 43 cells dug (40 of them the tunnel), stone 22 stored by day 3. Tests: `SurvivalScenarioTests.Seed1_SurvivalScript_AllAliveAtDay10` (tunnel dug, ≥ 10 stone stored before the breach), `DigScenarioTests.{DigStone_EndsInHubStorage, Tunnel_DugFromExposedSideInward}`. |
| 7 | Dig through the bank; tunnel floods; dwarves in deep water path out; levees wall the breach, flood stops | Headless: 2 breach levees complete by day 4 (7 levees); 15,125 units stay in the tunnel through the drought. Tests: `SurvivalScenarioTests.Seed1_SurvivalScript_AllAliveAtDay10` (tunnel deep after the breach, a dwarf caught in the flood got out and lived, repair levees complete, tunnel keeps its water while the river beside it is 0), `FloodScenarioTests.AgentFleesRisingWater`, `LeveeScenarioTests.LeveeLine_StopsBreachFlood`, `WaterScenarioTests.BreachFloodsTunnel`. |
| 8 | Drought (source off 2 days); river drains; dry crops wither; a levee reservoir keeps its pump running | Headless: season Drought days 5-7, world water 4.9 M → 15,125, 41 crops withered, pump NoWater on 4,712 drought ticks, the colony lives on stored water (110 → 90). Tests: `RiverTests.DroughtDrainsRiver`, `WeatherTests.*`, `FarmScenarioTests.DryForADay_Withers`, new `ReservoirScenarioTests.LeveeReservoir_PumpRunsThroughDrought` (+ control `OpenPool_PumpRunsDryInDrought`). Screenshot `farm13200/farm.png` (drought HUD, empty river bed, straw crops). The survival session itself has no reservoir pump (ADR-053). |
| 9 | All dead of hunger or thirst → "Colony lost", no win screen | Tests: `SurvivalScenarioTests.Seed1_NoCommands_ColonyLost` (seed 1 without commands, all Dehydrated before day 7), `StarvationScenarioTests.AllDead_ColonyLostOnce`, `BuildingViewTests.Alerts_NoFoodNoWater_ThenColonyLost`. Screenshots `lost/` (M5-T6). |
| 10 | Save at any point, quit, load, continue with identical results | Tests: new `SurvivalScenarioTests.Seed1_SurvivalScript_SaveLoadMidSession_ContinuesIdentically` (save at tick 7,400 in the flooded tunnel, load, play on to 12,600: same hash, repair levees built after the load), `SaveLoadTests.{RoundTrip_FutureEqual, MidFlee_RoundTrip_ContinuesIdentically, SaveSize_Day5_Under3MB}`, `WeatherTests.SaveLoad_MidDrought_MatchesContinuousRun`, `GoldenHashTests.*`. |
| – | All tests green | check.sh 513 passed, 0 skipped; perf.sh 8 of 8 within budget. |

- Screenshots: none new (no rendering or Godot code changed); the table cites screenshots of earlier tasks. The
  harness still cannot run `SCRIPT=survival` (it enqueues every command at tick 1).
- Weak spots for G3: the survival pump is on the open river and is dry for the whole drought plus ~300 ticks of
  refill (the colony relies on 110 stored water); replanted crops all wither in the drought (41 withers, no second
  harvest by day 10); berries fall 69 → 39 while 105 potatoes sit untouched to day 10; region rebuilds are ~77% of
  tick time while the river drains (SIM-P1 2.46 ms of 8).
- Next: M6-GATE (HUMAN-GATE G3: POC review).

## M6-GATE — HUMAN-GATE G3: POC review (2026-09-26)

**Status: waiting for human review. The gate box in BACKLOG.md is NOT checked; check it after review.**
All M0-M6 tasks are checked. The backlog has nothing after this gate. No code changed for this report.

### Verdict in one paragraph
All ten steps of the definition-of-done session (docs/00-overview.md) have evidence on seed 1 (M6-T8 checklist,
above). The scripted survival session ends with all 5 dwarves alive at day 10 and 0 failed jobs. It is
deterministic (same hash on every run, and after a save/load mid-flood). Every perf budget passes with at least
3x headroom. The weak points are balance and scripting, not correctness. The survival pump sits on the open river,
so it is dry for the whole drought. Every drought kills every crop. Seed 1 needs a hand-dug notch before a pump can
reach water. The scripted session has no levee reservoir (that part of DoD 8 is shown by a synthetic scenario).
The POC has not yet been played hands-on end to end by a human.

### Build and test results (this run, HEAD = M6-T8 `1f011bf`)
- `./scripts/check.sh`: **OK**. 513 passed, 0 skipped, 0 failed (non-Perf, Debug). Godot csproj 0 warnings.
- `./scripts/perf.sh`: **OK**. 8 of 8 passed (Release). SAV-05 (save size) runs in check.sh and passed.

| ID | What | Budget | Measured | Use |
|---|---|---|---|---|
| WAT-P1 | water step, 192×128 sustained (≥ 23,048 active) | ≤ 4 ms | median 1.29 ms, p95 1.34 | 32% |
| WAT-P1 | water step, 128×128 (15,494 active) | ≤ 4 ms | median 0.86 ms, p95 0.92 | 22% |
| WAT-P2 | seed-1 settled river, active cells at tick 1200 | ≤ 3,000 | 699 (step median 0.031 ms) | 23% |
| PTH-P1 | A*, 200 paths of 90..110 cells | p95 ≤ 1.5 ms | median 0.246, p95 0.626, max 0.796 ms | 42% |
| PTH-P2 | region rebuild, seed 1 | ≤ 25 ms | warm 2.10 ms, cold 2.39 ms | 10% |
| ECO-16 | moisture recompute | ≤ 3 ms | median 0.875 ms | 29% |
| MESH-P1 | chunk (3,0,2), 581 quads | ≤ 6 ms | median 0.639 ms, p95 0.724 | 11% |
| SIM-P1 | full tick, survival script, ticks 12,000-12,499 (drought) | median ≤ 8 ms | median 2.344, p95 2.539, max 3.46 ms | 29% |
| SAV-05 | save at day 5 | ≤ 3 MB | 31,781 bytes (M6-T7) | 1% |

Nothing is within 20% of its limit. SIM-P1 breakdown: region rebuilds 905 ms of the 500 ticks (407 rebuilds, ~77%
of the tick), water 61 ms. Everything else is under 0.1 ms per tick.

### Headless runs (this run)
- `./scripts/run-headless.sh --seed 1 --script survival --ticks 24000`: hash **`7344b913cc2909c9`** (same as
  M6-T6/T7/T8), 3,543 ticks/s, tick median 0.071 ms, p95 1.17 ms. **5/5 alive at day 10**. 406 jobs done,
  **0 failed**. 43 of 43 cells dug, 23 of 23 trees felled. 0 items on the ground. 35 crops harvested (105 potatoes)
  and 41 withered. The pump was dry on 5,004 ticks (4,712 in the drought, 292 during the refill). 568 path searches,
  978 region rebuilds, 0 trapped. Stored at day 10: log 76, stone 22, berries 39, potato 105, water 110.
  The per-day table is in the M6-T8 entry and did not change.
- `./scripts/run-headless.sh --seed 1 --ticks 24000` (no commands): the colony is lost at tick 15,009 (day 6).
  0 of 5 alive, hash `6cef6aa7d84e85dd`, 5,871 ticks/s. DoD 9: without a pump the colony dies of thirst in the
  first drought.

### Screenshots (Godot 4.6.2 .NET, Forward+, seed 1; rendered for this gate and looked at)
All shots are under `artifacts/screens/g3/`.
- `default/{overview,river,hub,slice}.png` (1,200 ticks, no commands). The overview shows the whole map: the
  river runs from the west edge to the east edge, with the hill and trees, and the Great Hall is a small brown box
  by the river. The top bar reads "Day 1 · Wet season, 5 days left · Log 30 · Berries 68 · Water 30". The slice
  shot cuts at a z-level and shows stone lenses in the dirt (gray) and the river channel in section.
- `digchop1200/hub.png`. The pit east of the hall is dug (brown floor). Orange chop rings sit on the marked trees,
  "4" log piles lie around the hall, and all 5 dwarves are "Felling a tree".
- `build1600/{hub,river,overview,slice}.png`. The build script's warehouse and pump are next to the hall, and the
  dwarves are "Hauling log" (Log 56). **The build-script pump shows a red "NO WATER" billboard and the top bar
  shows "Pump has no water".** This script picks the nearest valid site, and on seed 1 no valid site has water
  (ADR-044). The harness also shows a fixed red warehouse ghost on the hall roof ("Needs solid ground under it").
  **Its tooltip overlaps the NO WATER billboard**, so both are hard to read (a view nit).
- `farm4000/farm.png` (day 2): a 5×5 field on two terrain levels near the bank, with young green crops in every
  tile and berry bushes with red berries.
- `farm9000/farm.png` (day 4): the harvest is done (Potato 75, Berries 77). All 5 dwarves are "Planting" on bare
  furrows, and the first new sprouts are up.
- `farm13200/{farm,overview}.png` (day 6): the top bar reads "Drought, 2 days left" in orange, and a toast says
  "Drought: the springs stop for 2 days and the river drains". **The river bed is empty sand across the whole
  map.** The crops are straw colored. The farm script has no pump, so "Water 0 · No water" is shown in red and the
  drink bars are low. That is expected for this script, and it is DoD 9 on the way.
- No shot of the survival session itself (tunnel flood, breach levees). The screenshot harness cannot run
  `SCRIPT=survival` because it enqueues every command at tick 1. That session is covered by tests and the headless
  stats.

### What works
- **World and water**: seeded 128×128×64 terrain with a hill and a river. The fixed-point water CA holds the
  river's volume for 10 days, floods what is dug, drains in a drought and refills after it. Levees wall a breach.
- **Colonists**: A* with regions, jobs with reservations and a failure cooldown, digging (pits keep a way out:
  G2 rule, ADR-037), chopping, hauling to the nearest storage, fleeing from deep water, eating and drinking, and
  death with a Colony-lost screen.
- **Buildings**: blueprint → deliver → construct → complete, cancel and deconstruct, warehouse overflow, pump with
  NoWater, stackable levees.
- **Farming and weather**: a moisture map, potatoes grow only on moist tiles, dry crops wither, bushes regrow and
  are harvested below a food target, and a 5-day wet / 2-day drought cycle.
- **Save/load and determinism**: identical hash after a save/load mid-flood and mid-drought, and golden hashes
  at 0/1200/3000/6000.
- **View**: greedy-meshed chunks, slicing, a water surface, tools (dig, chop, farm, build with ghost and reasons,
  deconstruct, cancel), a HUD with the season, alerts and the colonist panel, and a screenshot harness.

### What is weak (with evidence)
1. **Seed-1 pump needs a dug notch** (ADR-044). The banks step up one level per cell, so any pump whose intake
   is over water has its entrance inside the next bank step (`EntranceBlocked`). 0 of 1,921 valid pump sites are
   wet. A player must dig one cell first, and the script does (tick 0 notch at (40,18,79)). A new player will not
   discover this.
2. **Pump placement allows any drop-off** (ADR-040). BLD-03 only asks that the intake cells be non-solid, so a
   pump may stand on a dry ledge and just flag NoWater (see `build1600/hub.png`). The ghost is green there.
3. **The survival pump is dry for the whole drought**. It draws from the open river: NoWater on 4,712 drought
   ticks plus 292 during the refill. The colony lives on stored water (110 → 90). DoD 8's reservoir pump is
   proven only by the synthetic `ReservoirScenarioTests` (ADR-053), not in the seed-1 session.
4. **Every drought withers every crop**. The river drains fully, so every farm tile dries out. The run has 41
   withers and no second harvest by day 10 (the first harvest of 35 tiles gave 105 potatoes before day 5).
   Farming currently survives only if the first crop is in before day 5.
5. **Dwarves eat berries while potatoes sit unused**. Food choice is the lowest item id with stock, so berries
   come before potatoes (`NeedsSystem.PickItem`). Berries fall 69 → 39 while 105 potatoes are untouched through
   day 10. This is harmless now, but bush harvest only runs while food in storage is below 60, and potatoes count
   toward that target.
6. **Unreachable recurring jobs fail 5 times and are reposted forever** (ADR-030). Only digs and chops get an
   "unreachable" mark. A haul or delivery job that can never succeed keeps cycling with a fresh failure count.
   There were 0 failures in the survival run, so this has not been seen in play.
7. **The strand rule protects only the digger** (ADR-037). Other dwarves in a pit stay connected through the
   digger's side in practice, but nothing guarantees it.
8. **Region rebuilds are ~77% of tick time while the river drains or refills** (SIM-P1 2.34 ms of 8). Each
   water depth crossing changes walkability, so the tick does a full flood fill (~2.2 ms). This is fine at 5
   dwarves on 128²; incremental regions are the lever if maps or colonies grow.
9. **`BuildingAt` and overlap checks scan all buildings** (ADR-040). This is fine for a few dozen buildings.
10. **View nits**: the harness's ghost tooltip overlaps a building's billboard (`build1600`). The survival
    session cannot be screenshotted. Dwarves are plain white capsules (no name or job marker in the world).
11. **Not played by hand**. Every DoD step is proven by scripts and tests; the Godot build has had no full
    hands-on session since G1's follow-up.

### ADRs made during the build (53)
- Scaffold: 001 prefab construction / indirect control, 002 Godot 4.6 .NET + Godot-free sim, 003 data embedded in
  Sim, 004 integer state + fixed-point water, 005 thirst + pump in MVP, 006 `Aurvangar.Client` namespace, 007 Godot
  project outside the sln, 008 name and theme.
- M1-M3 (world, water, view): 009 river channel geometry, 010 water active set, 011 spread/film rules, 012 basin
  source, 013 water consumes world changes twice per tick, 014 pre-settle, 015 radix sort water step, 016 slice
  "cut" faces, 017 water mesher sides + depth tint, 018 view loop in ViewCore, 019 camera/picking, 020 screenshot
  presets, 021 river springs hold volume, 022 ambient light, 035 Forward+ screenshots.
- M4 (colonists): 023 PathGrid flag cache, 024 A* rules, 025 regions, 026 path following, 027 WorldActions and
  piles, 028 job board, 029 designations, 030 hauling, 031 flee/trapped, 032 save format v1, 033 Godot tools,
  034 PTH perf measurement, 036 headless scripts, 037 no-strand digs (G2), 038 tree-floor digs (G2), 039 pile
  markers (G2).
- M5 (buildings, needs): 040 placement validation, 041 construction flow, 042 pump, 043 needs, 044 build tools and
  HUD, 045 SurvivalScript + pump notch.
- M6 (farming, weather, integration): 046 moisture map, 047 farm tiles, 048 bush harvest, 049 weather, 050 farm
  view, 051 survival script, 052 perf harness + packed A* key, 053 DoD evidence.

### Questions for the human
1. **POC go/no-go.** Do you accept the POC as done on this evidence (DoD 1-10, all tests green, all budgets
   green)? I recommend a short hands-on session in the Godot build first (weak point 11), to judge the feel,
   which scripts cannot.
2. **Pump placement (ADR-040)**: keep "a pump may stand at any drop-off and flags NoWater", or require water at
   the intake when placing it? The second would make the ghost red on dry ledges, but a player could then not
   pre-place a pump by a reservoir they have not flooded yet.
3. **Pump notch on seed 1 (ADR-044)**: options are (a) keep it and teach it (a ghost reason such as "Entrance
   blocked: dig the bank step"), (b) let the pump use a stand cell one level up (like stacked levees), or (c) terrace
   the generated banks so some sites work out of the box. I recommend (b).
4. **Drought and farms**: every drought withers every crop. Is that the intended pressure, or should moisture
   also come from player-held water (reservoirs, flooded tunnels), so a planned colony can farm through a drought?
   I recommend the latter; it makes DoD 8's reservoir matter for food as well as drink.
5. **Survival script reservoir**: should the seed-1 survival session build a real levee reservoir for its pump
   (DoD 8 in the session itself, not only the synthetic scenario)? This would regenerate the goldens.
6. **Food choice**: eat the most plentiful food first, or the one that spoils (berries), or keep lowest-id?
   Potatoes currently sit unused.
7. **Unreachable jobs (ADR-030)**: add a give-up mark after N reposts, with a HUD notice, for haul and deliver
   jobs? Or leave it until it shows up in play?
8. **Strand rule scope (ADR-037)**: extend it to every dwarf, or keep digger-only?
9. **Performance**: region rebuilds dominate drought ticks (77%). Do incremental regions now, or only when maps
   or colony sizes grow? `BuildingAt` indexing: same question. I recommend deferring both.
10. **Next milestone priorities.** My recommendation, in order:
    - **M7 — Playability pass** (small, before any new system): pump-site fix (Q3), drought farming via reservoir
      moisture (Q4), food choice (Q6), give-up marks for unreachable jobs (Q7), label overlap, the screenshot
      harness running timed scripts (survival), and a hands-on DoD session report.
    - **M8 — Scale and robustness**: incremental region updates, a building spatial index, a larger map (e.g.
      256²) with its perf budgets, save-format versioning.
    - After that, the long-term direction (science, exploration, diplomacy) per docs/00-overview.md needs a
      milestone plan from you. No backlog task adds those systems until then.

Next after approval: whatever tasks the answers add. The backlog is otherwise empty.

### G3 answers (human, 2026-09-26)

- **Hands-on play (Q1):** the human played the Godot build and called it "a great start". POC accepted; gate G3
  closed. Feedback:
  - "I find I am right-clicking to pan and keep forgetting it is center mouse." -> M7-T1.
  - "It feels a little like there is not much for me to do. I want to build great constructs, I want to plan
    monuments." Asked what that means, the human chose **free-form block building**: paint walls, floors and
    shapes block by block, with dwarves hauling material and building it. -> new milestone M8. This amends
    ADR-001 (prefab-only); prefab functional buildings stay.
- **Q2-Q10:** the human took the recommendations.
  - Q2: keep allowing pumps on dry ledges (they flag NoWater).
  - Q3: (b) the pump may use a stand cell one level up -> M7-T2.
  - Q4: yes, player-held water keeps fields moist -> M7-T3.
  - Q5: yes, the survival session builds a real levee reservoir -> M7-T3 (regenerate goldens).
  - Q6: eat the most plentiful food first -> M7-T4.
  - Q7: add a give-up mark with a HUD notice -> M7-T5.
  - Q8: the strand rule protects every dwarf -> M7-T6.
  - Q9: defer incremental regions and the building index.
  - Q10: M7 playability pass (plus right-drag pan, label overlap, timed screenshot scripts), then M8 free-form
    construction and monuments, ending at gate G4.

## M7-T1 — Right-drag pans the camera (2026-09-26)
- Done: right-drag now moves the camera exactly like middle-drag (orbit: yaw and pitch, `OrbitRig.Drag`); middle-drag
  is unchanged. New `ViewCore.Camera.ClickDragGesture` tells a right click from a right drag (net displacement
  > 4 px since the press; the crossing motion hands over the accumulated delta). `CameraRig` feeds it and raises
  `RightClicked` on a click, which GameRoot wires to `ToolController.AbortDrag` (the abort moved from right press to
  right click release, so a right-drag no longer kills a left-button dig box). VIEW-06 in `docs/specs/view-ui.md`
  documents it. There is no in-game controls text to update.
- Tests: new `View/ClickDragGestureTests` (8): click without movement, jitter within the threshold, drag past it,
  accumulated delta on crossing, net displacement for back-and-forth jitter, no press, re-press reset, and a
  right-drag giving the same yaw/pitch as the same middle-drag motion. They failed to compile before the class
  existed. check.sh: 521 passed, 0 skipped, 0 failed; Godot csproj 0 warnings.
- Decisions: ADR-054 ("pan" read as the middle-drag camera move the human named; 4 px threshold).
- Golden: unchanged (no sim change).
- Perf: n/a (view input only). Screenshots re-rendered (`artifacts/screens/{overview,river,hub,slice}.png`) and
  looked at: unchanged scene, HUD intact. The harness cannot inject mouse input, so the drag itself is covered by the
  ViewCore tests only; worth a quick hands-on check at G4.
- Next: M7-T2 (pump entrance one level up). If the human meant a grab-the-map translation rather than orbit, feed
  `ClickDragGesture` into a new `OrbitRig` pan method instead of `Drag` (ADR-054).

## M7-T2 — Pump entrance may stand one level up (2026-09-26)
- Done: a `waterEdge` building (the pump) whose entrance cell is not free may use the standable cell one level above
  it (on a stepped bank the entrance is inside the next bank step). `CanPlace` accepts it, the cell above a pump's
  entrance is reserved against footprints, `Construction.StandCell` returns it (deliveries, refunds, agents moved out
  of a site), and the OperatePump job walks to it (reposted if the stand cell moves). New read-only
  `BuildingSystem.PlannedStandCell`, used by the build ghost's white stand tile. BLD-03/BLD-13 updated in
  docs/specs/buildings.md. The screenshot `build` script now places its pump at the nearest wet site.
- Tests: new `Scenarios/PumpSiteScenarioTests` (2): seed 1 has wet pump sites reachable from the hall without any dig
  (354, all using the raised cell; was 0), and the colonists build a pump at the nearest one (entrance cell solid) and
  work it from the raised cell, with water hauled and 0 failed jobs. New `BuildingPlacementTests.Pump_EntranceInside
  BankStep_UsesTheStandCellOneLevelUp` (stepped bank: Ok with the raised cell, blocked headroom still rejected, a
  levee still rejected, the raised cell reserved). `ScreenshotScriptTests` build test now asserts the pump has water.
  All failed before the change for the right reason (EntranceBlocked / no sites). check.sh: 524 passed, 0 skipped,
  0 failed; Godot csproj 0 warnings.
- Decisions: ADR-055. The SurvivalScript keeps its tick-0 notch (with it dug the entrance is standable, so nothing
  changes); the nearest raised site is the script's own pump site (40,18,80), so M7-T3 may drop the notch if it wants.
- Golden: unchanged. Headless `--seed 1 --script survival --ticks 24000`: hash `7344b913cc2909c9` (same), 5,558
  ticks/s, 5/5 alive, pump dry 5,004 ticks (unchanged; M7-T3 adds the reservoir).
- Perf: perf.sh 8/8 passed (run serially). No path code changed; the extra placement check is one standable lookup.
- Screenshots: `artifacts/screens/m7t2/{hub,river,overview}.png` (SCRIPT=build, TICKS=1600), looked at. The pump
  now stands at the river's edge, Althjofr is "Working the pump", and the top bar no longer shows "Pump has no
  water"; the NO WATER billboard (and its overlap with the hub ghost tooltip) is gone from `hub`. The first render
  after the ViewCore edit came out stale (identical to the old pump); a second run rendered the change.
- Next: M7-T3 (levee reservoir for the survival pump and farm). The pump may now be placed on any stepped bank edge
  without a notch; `ScreenshotScripts.FindSite(..., wet: true)` finds wet sites.

## M7-T3 — Survival session builds a levee reservoir for its pump and farm (2026-09-26)
- Done: `SurvivalScript` now digs a one-wide reservoir trench beside the farm (x = 67, z 61..70, water layer y = 17),
  dammed from the river until tick 1200. It then opens the dam to fill the trench and seals the mouth (67,17,71) with a
  levee at tick 9600, which completes at ~11,700, before the drought. The pump moved to the reservoir's south end
  (pad (68,18,70), rot 90, raised stand per ADR-055), and the tick-0 notch is gone. The farm narrowed to 5×6 = 30 tiles
  (x = 67 is now the reservoir). No sim change: ECO-15 moisture already counts held water >= 128. The docs/testing.md
  "Scripted play" table was updated.
- Tests: `ReservoirScenarioTests` gains a dirt field beside the pool.
  - New `FieldBesideLeveeReservoir_GrowsThroughDrought`: 0 dry tile-ticks and 0 withers in the drought, every tile
    Growing or Mature at its end, harvests during it, and the pump never NoWater.
  - New control `FieldBesideOpenPool_DriesOutInDrought`: every tile is dry by the drought's end.
  - New `SurvivalScenarioTests.Seed1_SurvivalScript_LeveeReservoirCarriesPumpAndFieldThroughDrought`, checked at
    several points:
    - Tick 1200: the trench is dry and dammed, and the pump is complete but NoWater.
    - Tick 2400: the pool is full and the pump has water.
    - Day 5: the seal is complete.
    - Day 6: the river is empty and the pool still holds water.
    - Day 7: pump dry ticks in the drought <= 30 (measured 0), and 0 dry field tile-ticks.
    - Day 10: 0 withers, and all 30 tiles harvested at least twice, with the second harvest after day 5. All 5
      dwarves are alive, and there are no rejects.
  - Adjusted assertions:
    - Farm tile count 36 -> 30 (the column is now the reservoir).
    - `SurvivalScriptTests` hub water at tick 3000 changed from > 50 to > 30, measured 36. The pump starts after the
      fill (~1,300), not at ~800 (ADR-056).
  - check.sh: 527 passed, 0 skipped, 0 failed; Godot csproj 0 warnings.
- Decisions: ADR-056.
- Golden: regenerated. The survival command log changed (reservoir digs, pump site, farm box, seal levee), so every
  checkpoint after tick 0 moves:

  | Tick | Old | New |
  |---|---|---|
  | 1200 | 278032913bfc4cea | af96102eab104c7a |
  | 3000 | 25f61178eeb2b39f | 8283dd6fdae77ad6 |
  | 6000 | 01b5005c23875f6c | 8fdcd9964d3947f1 |

  The tick-0 hash `2dce28cc9774b9be` is unchanged.
- Headless `--seed 1 --script survival --ticks 24000`:
  - Hash `b8ecd61909bad882` (was `7344b913cc2909c9`), 5,225 ticks/s. 5/5 alive.
  - Jobs: 444 completed, 0 failed.
  - Crops: 60 harvested (was 35), 0 withered (was 41).
  - Pump dry ticks: 855, all on day 1 before the fill. 0 in the drought (was 4,712).
  - Stored at the end: potato 180, water 110.
- Perf: perf.sh 8/8 passed.

  | Test | Measured |
  |---|---|
  | WAT-P1 | 1.30 ms (23,048 active); 0.88 ms at 128×128 |
  | WAT-P2 | 699 active |
  | ECO-16 | 0.873 ms |
  | SIM-P1 | median 2.338 ms, p95 2.563 ms |
  | PTH-P1 | p95 0.745 ms |
  | PTH-P2 | 3.19 / 3.48 ms |
  | MESH-P1 | 0.641 ms |
- Screenshots: none (no rendering or Godot change).
- Next: M7-T4 (eat the most plentiful food first; potatoes now pile up to 180). M7-T7's timed screenshot scripts
  should include a shot of the reservoir (x = 67, z 61..71) and the farm beside it.

## M7-T4 — Dwarves eat the most plentiful food first (2026-09-26)
- Done: `NeedsSystem.TryPost` now picks the item first, then the storage. The item is the one that restores the need
  with the most unpromised units summed over the complete storages in the agent's region, with ties going to the lower
  item id (berries). The storage is the nearest reachable one holding that item (Manhattan, ties by lower building id,
  as before). Drink uses the same rule (only water today). `PickItem` is replaced by `HasItem`/`Restores` (the HUD
  NoFood/NoWater query is unchanged in meaning). ECO-04 in docs/specs/needs-economy.md is updated. No new state, so
  no save or hash change.
- Tests: two new tests in `NeedsTests`, both failing before the change (berries picked by lowest id):
  - `Eat_PicksMostPlentifulFood`: 6 berries and 12 potatoes give the meal order `PPPBPPBP`. Potatoes are eaten while
    they outnumber berries, and berries on a tie.
  - `Eat_CountsFoodAcrossReachableStorages`: the hub holds 10 berries and 2 potatoes, and a farther warehouse holds 10
    potatoes. The meals are potato from the hub, then berries from the hub (tie), then potato from the warehouse.
  - Fixed `FarmScenarioTests.Harvest_StorageFull_DropsPile` setup (reason in ADR-057). The hungry dwarf ate 2 of the
    100 hub potatoes, so the hub was no longer full. Berries are now topped up to the cap, and the assertions are
    unchanged.
  - check.sh: 529 passed, 0 skipped, 0 failed; Godot csproj 0 warnings.
- Decisions: ADR-057 (unit counts rather than food value; item first, then nearest storage; test fix).
- Golden: unchanged. The golden checkpoints (ticks 0..6000) come before any potato meal would differ, so no
  regeneration was needed.
- Headless `--seed 1 --script survival --ticks 24000`:
  - Hash `4caa8837b195f900` (was `b8ecd61909bad882`), 5,355 ticks/s. 5/5 alive.
  - Jobs: 441 completed (was 444), 0 failed.
  - Crops: 60 harvested, 0 withered. Pump dry ticks: 855 (0 in the drought).
  - Stored at the end: potato 160 (was 180), berries 69 (was 39), water 110.
  - Potatoes still pile up because harvests out-produce 5 dwarves. With food >= 60 the bushes stop being harvested
    (ADR-048), so berries now sit in storage instead of being eaten first.
- Perf: perf.sh 8/8 passed.
- Screenshots: none (no rendering or Godot change).
- Next: M7-T5 (give-up mark for jobs that never succeed).

## M7-T5 — Give-up mark for jobs that can never succeed (2026-09-26)
- Done: new JOB-12 give-up marks (`Jobs/GiveUpMarks.cs` state, `Jobs/JobGiveUp.cs` system).
  - Sources: site deliveries, pump OperatePump, pump buffer haul, pile haul.
  - A strike is a JOB-08 cancellation at the fifth failure, or a 50-tick check that finds the source's open job out of
    every living agent's region (such a job is never claimed, so it never failed or reposted before).
  - At 3 strikes the source is given up. Its poster (`Pumps`, `Construction.KeepDelivers`, `HaulSystem`) withdraws
    its open jobs and posts none.
  - A mark goes when a job of the source completes, a walkability change lands within 8 cells, a storage completes
    (all marks), or the source is gone. `PathGrid.WalkChanges` records the changes; it is cleared each tick.
  - `JobGiveUp.Tick` runs in step 11 after the region rebuild.
  - HUD: `TopBarModel.GiveUpText` adds "Unreachable: Water Pump, log pile x2" to the alerts. The Godot top bar already
    shows all alerts, so there is no Godot change.
  - Save format v4 -> v5 (new `GiveUps` section). Marks are hashed when any exist.
  - Specs: JOB-12 added (and JOB-08 points to it); VIEW-15 and SAV-01 updated. The task cites JOB-09, but the
    relevant rule is JOB-08 (retry and cancel).
- Tests: new `Scenarios/GiveUpScenarioTests` (5). They failed to compile before the feature existed.
  - `UnreachablePumpEntrance_GivesUp_ShowsNotice_UntilBridgedNearby`: a trench cuts the pump off from the dwarves.
    - The pump is given up at tick 101 with 0 failed jobs, and the alert shows.
    - No OperatePump job is posted for 600 ticks.
    - A far block change does not reset the mark.
    - Bridging the trench 7 cells from the stand cell resets it, and water reaches the hub.
  - `GivenUpMark_IsSavedAndHashed`: save and load keep the hash equal, the mark is in the hash, and 300 more ticks
    match.
  - `UnreachablePile_HaulGivenUp`: a pile in the trench gets no haul and the alert names it; the mark goes with the
    pile.
  - `RepeatedlyFailingDelivery_GivenUp_NewStorageResets`: the test takes the hub's logs whenever a delivery is
    claimed.
    - After 3 cancellations the site is given up (15-19 failures; the site's second load fails in between).
    - No deliveries are posted for 500 ticks.
    - A second warehouse completing resets the mark, and the site gets built.
  - `Success_ClearsStrikes`.
  - `UnreachableMark_RecoversWhenReconnectedFarAway`: a bridge 20 cells away does not reset the mark directly, but
    the next region check drops it, and water reaches the hub.
  - check.sh: 535 passed, 0 skipped, 0 failed; Godot csproj 0 warnings.
- Decisions: ADR-058.
- sim-reviewer found three things, all fixed:
  - A save/load hash split. On load, restoring plants and sites filled `WalkChanges`, which reset nearby marks on the
    first tick. `AfterLoad` and `WorldFactory` now clear it. The save test now has a bush beside the pump; I checked
    that it fails without the fix.
  - Given-up unreachable sources never recovered from a far-away reconnection. The region check now drops them.
  - Strikes were not capped; they now stop at 3.
- Golden: unchanged (no marks arise in the seed-1 session; an empty mark set adds nothing to the hash).
- Headless `--seed 1 --script survival --ticks 24000`:
  - Hash `4caa8837b195f900` (unchanged), 5,169 ticks/s. 5/5 alive.
  - Jobs: 441 completed, 0 failed.
- Perf: perf.sh 8/8 passed.

  | Test | Measured |
  |---|---|
  | WAT-P1 | 1.27 ms (23,048 active); 0.85 ms at 128×128 |
  | WAT-P2 | 699 active |
  | ECO-16 | 0.857 ms |
  | SIM-P1 | median 2.348 ms, p95 2.511 ms |
  | PTH-P1 | p95 0.612 ms |
  | PTH-P2 | warm 2.06-4.28 ms, cold 3.66-6.53 ms |
  | MESH-P1 | 0.642 ms |

  PTH-P2 varies run to run on the same build (2.06 then 4.18 ms warm). The pre-change tree measured 2.10 ms. The
  rebuild path is untouched, and the SIM-P1 region phase is the same as before (906 vs 899 ms over 409 rebuilds).
- Screenshots: none. No rendering or Godot code changed; the notice goes through the existing alert label.
- Next: M7-T6 (the strand rule protects every dwarf). Also:
  - `PathGrid.WalkChanges` is a per-tick list of the cells whose walkability changed. Reuse it for anything that
    needs "the world changed near X".
  - A new recurring job type must be added to `JobGiveUp.SourceOf` and must check `sim.GiveUps` in its poster.

## M7-T6 — No dig strands any dwarf (2026-09-26)
- Done: the ADR-037 strand rule now protects every living dwarf, not only the digger.
  - A dig is not taken, started or finished while it would cut another dwarf off from the Great Hall. The dwarf's
    cell (or the cell it is stepping into) must reach the hall now and not after the dig.
  - Where: `JobRunner.Select` (last filter), Work start and the Dig step (stand down, no failure). The logic is in
    `DigStrand.StrandsOthers`.
  - One check per candidate dig. New `DigTrial.MayCut` (`Paths/DigTrial.Pockets.cs`) computes the cells a dig cuts
    off and caches them per block until walkability changes (with the `MaySplit` cache). It floods from each move
    neighbor of the cell on top of the dug block against the hall anchors; neighbors met by an earlier flood share the
    answer. Only digs that `MaySplit` flags pay for this. The two-sided flood now reports which side ran out.
  - Deadlock guard: `DigStrand.StepOut`. An idle dwarf with nothing to do whose cell an open dig would cut off walks
    towards the hall (two dwarves in one pocket would otherwise each hold the dig up for the other).
  - DSG-09 give-up is unchanged: a dig held up only by another dwarf waits and is never turned red for it.
  - DSG-09 and JOB-09 specs updated. No new state, so no save or hash change.
- Tests: all failed before the change (the scenarios with a dwarf cut off at tick 79).
  - New `Scenarios/StrandAnyDwarfScenarioTests` (3):
    - `SecondDwarfWorkingInsidePit_GapDigWaitsUntilItLeaves`: a walled yard whose only way in is a gap with a shaft
      under its floor. One dwarf digs out the yard and hauls its stone while the other is sent to dig the gap from
      outside. The gap is dug only after the yard, nobody ever leaves the hub region, and there are 0 failures.
    - `IdleDwarvesInsidePocket_WalkOut_ThenGapIsDug`: fails without `StepOut` (checked).
    - `DwarfAlreadyApart_DoesNotBlockDig`.
  - New `DigTrialTests.ExitStep_StrandsOtherDwarfInTrench`.
  - check.sh: 539 passed, 0 skipped, 0 failed; Godot csproj 0 warnings.
- Decisions: ADR-059 (ADR-037 annotated).
- sim-reviewer: no required fixes. Two optional suggestions applied:
  - The cell on top of the dug block never counts as cut. DSG-08 owns it, and it is now the same in both flood
    outcomes.
  - A comment for the case where no anchor is walkable.
- Golden: unchanged.
- Headless `--seed 1 --script survival --ticks 24000`: hash `4caa8837b195f900` (unchanged), 5,210 ticks/s, 5/5 alive,
  441 jobs completed, 0 failed.
- Perf: perf.sh 8/8 passed.

  | Test | Measured |
  |---|---|
  | WAT-P1 | 1.30 ms (23,048 active); 0.86 ms at 128×128 |
  | WAT-P2 | 699 active |
  | ECO-16 | 0.876 ms |
  | SIM-P1 | median 2.361 ms, p95 2.533 ms |
  | PTH-P1 | p95 0.620 ms |
  | PTH-P2 | warm 2.14 ms, cold 2.44 ms |
  | MESH-P1 | 0.644 ms |
- Screenshots: none (no rendering or Godot change).
- Next: M7-T7 (readable labels, timed screenshot scripts). For M8: "no walling a dwarf in" can reuse
  `DigStrand.StrandsOthers`/`DigTrial.MayCut`, but a placed block removes a cell rather than a floor, so it needs a
  what-if view for placement. Deconstruction still protects only the worker (ADR-059).

## M7-T7 — Readable labels and timed screenshot scripts (2026-09-27)

- Done: the mouse label no longer covers building billboards, overlapping building labels are lifted apart, and the
  screenshot harness can run the timed survival session.
  - New `ViewCore/Hud/LabelLayout` (pure C#):
    - `PlaceTooltip` puts the mouse label (ghost tooltip, pile counts, farm hints) below-right of the point. If that
      covers a billboard it tries the other corners, then spots just above or below each billboard. The nearest clear
      spot on screen wins; with none it takes the least overlap.
    - `Declutter` lifts overlapping billboard labels so none overlap. The lowest label (the nearest) stays put.
    - `BillboardScale` and `BillboardRect` give the screen size of a fixed-size Label3D.
  - Godot:
    - `BuildingView.CollectBillboardRects` projects each label, NO WATER icon and progress bar. `Hud.SetHoverLabel`
      places the label with them.
    - `BuildingRenderer.Refresh` ends with a declutter pass (Label3D.Offset).
  - `ScreenshotScripts.Run` enqueues a timed script's commands at their ticks (`survival`, via `SurvivalScript.EnqueueDue`).
    `GameRoot.RunScriptNow` uses it and routes events in batches.
  - New presets:
    - `tunnel`: the hill tunnel and breach, sliced at y=18, from the east.
    - `reservoir`: the reservoir at x=67 and the farm.
  - Headless `--script` takes the same names list.
  - Docs updated: VIEW-14 and VIEW-20 (view-ui.md), testing.md "Screenshot presets", and the screenshot.sh header.
- Tests:
  - New `View/LabelLayoutTests` (11).
  - New `View/TimedScreenshotTests` (5). The `survival` harness run gives the same command log ticks and hash as
    `SurvivalScript.Run` over 1300 ticks, with no rejects. The survival presets frame the tunnel and the reservoir.
  - check.sh: 555 passed, 0 skipped, 0 failed; Godot csproj 0 warnings.
- Decisions: ADR-060.
- sim-reviewer: not run (no Aurvangar.Sim change).
- Golden: unchanged.
- Headless `--seed 1 --script survival --ticks 24000`: hash `4caa8837b195f900` (unchanged), 5,303 ticks/s.
- Perf: perf.sh 8/8 passed.

  | Test | Measured |
  |---|---|
  | WAT-P1 | 1.29 ms (23,048 active); 0.85 ms at 128×128 |
  | WAT-P2 | 699 active |
  | ECO-16 | 0.879 ms |
  | SIM-P1 | median 2.359 ms, p95 3.301 ms (one noisy full run: 4.18 ms) |
  | PTH-P1 | p95 0.652 ms |
  | PTH-P2 | warm 2.15 ms, cold 2.46 ms |
  | MESH-P1 | 0.649 ms |
- Screenshots (Forward+, `artifacts/screens/m7t7/`; run `dotnet build src/Aurvangar.Godot` first, because
  `--build-solutions` did not rebuild the game dll):
  - `build500`, `build1600`, `build150`: hub and river. Since M7-T2 moved the pump to a wet site, the build1600 ghost
    tooltip no longer overlaps anything. Avoidance was checked with an injected obstacle (temporary code, removed).
  - `survival3000/reservoir`: the reservoir full, beside the farm.
  - `survival7700/tunnel`: the flooded tunnel. The two breach levee labels ("Building Levee 25%" and
    "Levee: log 0/2") were drawn on top of each other and are now separated.
  - `survival8400/tunnel` and `overview`: the breach levees complete.
  - `survival14400/reservoir`, `river` and `tunnel`: the drought. The river bed is empty sand; the reservoir still holds
    water and crops; the tunnel is still flooded.
  - `survival7400/tunnel` is superseded (taken before the breach flooded).
- Next: M8-T1 (construction spec and ADR). Save format stays at v5.

## M8-T1 — Construction spec and ADR (2026-09-27)
- Done: M8 design, docs and placeholder tests only. No code changed.
  - New `docs/specs/construction.md`, with rules CON-01..18 and CON-P1:
    - Blocks: three construction blocks (`Masonry` 1 stone, `Planks` 1 log, `PolishedStone` 2 stone), ids 8..10,
      with new `blocks.json` fields `label`, `cost` and `buildTicks`, and ContentDb validation.
    - Plan entries: one store with `Planned` and `Released` states. Statuses are derived. Entries are saved (v6) and
      hashed only when present.
    - Shapes: Single, Line, Wall, Floor, HollowBox, and Stair (added: dwarves need a way up).
    - Support: down or sideways to ground; nothing hangs. A removal must not unground a built block.
    - Build order: bottom-up by the cell-below rule, and reachable-first by posting only Ready entries.
    - Build jobs are batched trips from storage (up to 10 units).
    - `PlaceTrial`/`PlaceStrand`: a what-if view for placement that mirrors `DigTrial`/`DigStrand`.
    - `GiveUpSource.Build` (JOB-12).
    - Water: a built block is a plain solid (WAT-12), so a built wall holds water like a levee.
    - Refunds: 100% on dig; `DesignateDeconstructBlocks` marks only built blocks.
  - Other specs: VIEW-21..23 (block tool, plan ghosts, material totals) in `view-ui.md`, and forward notes (tagged
    with their M8 task) in world, designations, jobs-agents, save-load, buildings (BLD-02 `PlannedBlocks`, BLD-09
    `SupportsBlocks`) and needs-economy (ECO-11 skip).
  - `docs/00-overview.md`: M8 scope, DoD step 11 (the monument), construction row and out-of-scope list, glossary.
    "Stairs and ladders" is clarified to mean movement features; a block staircase is fine.
  - BACKLOG: M8-T2..T6 now list their CON/VIEW ids (task map at the end of construction.md).
- Tests: new `PendingAcceptanceTests.cs` holds 36 placeholders:

  | Task | Placeholders |
  |---|---|
  | M8-T2 | 16: `BlockContentTests`, `BuildShapeTests`, `BlockPlacementTests`, `Scenarios/BlockBuildScenarioTests` |
  | M8-T3 | 5: `BlockDeconstructScenarioTests` |
  | M8-T4 | 4: `BlockPlanScenarioTests` |
  | M8-T5 | 6: `View/BlockToolTests` |
  | M8-T6 | 4: `MonumentScenarioTests`, plus 1 `Perf/MonumentPerfTests` |

  check.sh: 555 passed, 35 skipped (the perf placeholder is filtered out), 0 failed; Godot csproj 0 warnings.
- Decisions: ADR-061 (amends ADR-001, which is annotated).
- sim-reviewer: not run (no Aurvangar.Sim change).
- Golden: unchanged.
- Perf: n/a (no code change). Headless: not run (no code change; the M7-T7 hash `4caa8837b195f900` stands).
- Screenshots: none.
- Next: M8-T2. Notes for it:
  - `ContentDbTests` asserts `Blocks.Count == 8`; change it to 11 and say why.
  - `ContentDb.Validate` must also reject json ids without a `BlockId` value.
  - `BlockColors` iterates `Enum.GetValues<BlockId>()`, so new enum values need palette colours or they draw magenta.
  - Keep the existing natural-block `PlaceBlock` path (WorldActionsTests use it).
  - Bump `SaveGame.FormatVersion` to 6 with the `BlockPlans` section after `GiveUps`. Hash plans only when there are
    any.
  - Add `GiveUpSource.Build` to `JobGiveUp.SourceOf`.
  - The `DesignateBuild` record carries the `Plan` flag from the start, so the codec does not change in M8-T4.

## M8-T2 — Build-block designations and jobs (2026-09-27)
- Done: construction blocks Masonry/Planks/PolishedStone (ids 8..10, data-driven cost/label/buildTicks, CON-01/02);
  `BlockPlans` store (`Aurvangar.Sim.Blocks`, hashed, save v6 `BlockPlans` section); `DesignateBuild` with six
  shapes, CON-08 validity and CON-09 plan support, cancel via `CancelDesignation`; `BlockBuildSystem` (ARCH-01 step 8)
  posts batched Build jobs (fetch from storage, then GoTo/Work/Place per cell) with CON-05 statuses, re-check,
  skip, step-aside/step-out; `WorldActions.PlaceBlock` takes the cost for construction blocks (`Unsupported` added);
  CON-14 no-walling-in via `PlaceTrial`/`PlaceStrand` (shared `TrialFlood` with `DigTrial`); `GiveUpSource.Build`
  and the "Unreachable: Stone wall" notice; `PlannedBlocks` for building placement; farms skip cells under entries.
- Tests: 16 M8-T2 acceptance tests moved out of PendingAcceptanceTests into BlockContentTests, BuildShapeTests,
  BlockPlacementTests and Scenarios/BlockBuildScenarioTests(.Access); all pass. `ContentDbTests` block count 8 -> 11
  (CON-01 adds three block types; foreseen in ADR-061). Non-Perf suite: 571 passed, 19 skipped (M8-T3..T6), 0 failed.
- Decisions: ADR-062 (Blocks namespace, shared what-if flood, re-check re-plans, Build marks outside region recovery,
  step-aside/step-out on released entries, Build GoTo re-goal when its stand cell fills, test-world geometry, review
  fixes). construction.md CON-04, CON-12, CON-13, CON-14, CON-15 amended to match.
- Golden: unchanged (empty plan store adds nothing to the hash). Headless seed 1 survival 24000 ticks: hash
  4caa8837b195f900 (unchanged).
- Perf: perf.sh 8/8 passed (1 skipped, M8-T6). sim-reviewer: 3 required fixes applied (skipped-only Build job no
  longer clears its give-up mark; over-cap plan support = unsupported per CON-09; CON-15 spec line), plus strict
  ascending order in the BlockPlans save reader.
- Next: M8-T3 (deconstruct built blocks, CON-10/17/18). A free-standing Stair only keeps step 0 (steps are only
  diagonally supported) — lean stairs against a wall. Reviewer notes worth keeping in mind for M8-T5/T6: public
  `BlockPlans.StatusOf`/`CanPlan` rebuild scans per call (batch them for ghosts); `CancelHolder` rebuilds HeldCells per
  repainted cell; the 16384-cell plan-support cap rejects everything for very large connected plans; `HasMaterial`
  ignores stock promised to unclaimed Build jobs, so a status can read Ready while Post skips it.

## M8-T3 — Deconstruct placed blocks (2026-09-27)
- Done: CON-10, CON-17 and CON-18.
  - `Support.Depends(sim, cells)` (`Blocks/Support.cs`): removing the cells would unground a built block among their
    up/side neighbours. Depth-first grounding search, down step first, cap 4096 per neighbour (over = depends).
  - Digs: `DesignationSystem` posts no job for a mark a built block depends on (waits, never red); `JobRunner.Select`
    skips such a posted job; Work start and the Dig step stand down (cooldown, no failure). `WorldActions.Dig` returns
    `Blocked`.
  - CON-17: digging a built block drops its whole cost as one pile on the dug cell (Masonry 1 stone, Planks 1 log,
    PolishedStone 2 stone); the dig takes the block's hardness.
  - CON-18: new command `DesignateDeconstructBlocks(A, B)` (CommandCodec entry) marks only built blocks;
    `NothingToDeconstruct` when there are none. Shares the marking loop with `DesignateDig`.
  - BLD-09: `Deconstruct` of a complete building whose footprint holds up a built block is rejected with
    `SupportsBlocks` (after `BuildingOnTop`). A block placed against a building already being deconstructed holds the
    teardown back (last tick `Blocked`, the job stands down) until the block is gone.
  - No new state: no save or hash change. Spec notes in construction.md (CON-10, CON-18), buildings.md (BLD-09),
    designations.md (DSG-03).
- Tests: the 5 M8-T3 placeholders moved to `Scenarios/BlockDeconstructScenarioTests` with bodies, plus one more
  (`DeconstructingLevee_WaitsForBlocksLeaningOnIt`). 5 of 6 fail with `Depends` stubbed to false and the refund off
  (the sixth needs the new command, so it did not compile before). New helper `Support/Grounding.Floating` checks the
  CON-09 invariant independently of the sim code; the tower/bridge scenario asserts it every tick (M8-T6 can reuse
  it). check.sh: 577 passed, 14 skipped (M8-T4..T6), 0 failed.
- Decisions: ADR-063.
- sim-reviewer: not run (Aurvangar.Sim change about 120 lines, under the ~150 threshold).
- Golden: unchanged. Headless `--seed 1 --script survival --ticks 24000`: hash `4caa8837b195f900` (unchanged),
  5,429 ticks/s, 0 trapped.
- Perf: perf.sh 8/8 passed (1 skipped, M8-T6).

  | Test | Measured |
  |---|---|
  | WAT-P1 | 1.28 ms (23,048 active); 0.85 ms at 128×128 |
  | WAT-P2 | 699 active |
  | ECO-16 | 0.848 ms |
  | SIM-P1 | median 2.314 ms, p95 2.495 ms |
  | PTH-P1 | p95 0.608 ms |
  | PTH-P2 | warm 3.99 ms, cold 6.06 ms (budget 25 ms; this varies run to run, path code untouched) |
  | MESH-P1 | 0.623 ms |
- Screenshots: none (no rendering or Godot change).
- Next: M8-T4 (plan layer: `ReleasePlan`, `Needed`, Planned end to end). Notes:
  - A wide structure only comes down top-down by the DSG-04 bias; strict top-first holds only where support forces
    it (columns, cantilevers).
  - Refund piles can be left in mid-air when the block under them is dug later (as with natural digs); they are
    still hauled.
  - check.sh shows a pre-existing Godot CS0108 warning (`BuildingRenderer.Scale` hides `Node3D.Scale`, since M7-T7);
    it does not fail the build. Worth a rename in M8-T5.

## M8-T4 — Plan layer for monuments (2026-09-27)
- Done: CON-06 and CON-07 `ReleasePlan`, and the `Planned` state end to end (the stored state, save byte, hash and
  poster skip came with M8-T2).
  - New command `ReleasePlan(A, B)` (`Commands/BlockCommands.cs`, CommandCodec entry): every Planned entry in the
    inclusive box (corners in any order) becomes Released; `NothingToRelease` when there are none.
    `BlockPlans.Release` walks the entries, not the box, so a world-sized box is O(entries).
  - `BlockPlans.Needed(PlanState? state)`: `(ItemId, Count)` per cost item in ascending item id, summed over the
    entries each call (O(entries), no cache). `BlockPlans` now takes `ContentDb` in its constructor.
  - No new state: save format (v6) and hash unchanged.
- Tests: the 4 M8-T4 placeholders moved to `Scenarios/BlockPlanScenarioTests` with bodies (removed from
  PendingAcceptanceTests). With a no-op `ReleasePlan` and an empty `Needed`, 3 failed for the right reason (entries
  stayed Planned, totals 0, nothing built after release). `PlannedEntries_NotBuilt` passed from the start, because the
  M8-T2 poster already ignores Planned entries. check.sh: 581 passed, 10 skipped (M8-T5/T6), 0 failed.
- Decisions: ADR-064. construction.md CON-07 has a note on the box and on no re-validation.
- sim-reviewer: not run (Aurvangar.Sim change about 50 lines).
- Golden: unchanged. Headless `--seed 1 --script survival --ticks 24000`: hash `4caa8837b195f900` (unchanged),
  5,640 ticks/s, 0 trapped.
- Perf: n/a (no water, path or tick-loop change); perf.sh not run.
- Screenshots: none (no rendering or Godot change).
- Next: M8-T5 (Godot block tool, plan ghosts, material totals). Notes:
  - The HUD reads `sim.Plans.Needed(PlanState.Released)` and `Needed(PlanState.Planned)` against `Storage.Totals`
    (`BuildingSystem.Totals`, keyed by item id value).
  - "Release all" is `ReleasePlan((0,0,0), world max)`.
  - A Released entry is not re-validated. If the entry under it is still Planned, it shows `BelowFirst`, so a
    partial release of an upper slice waits for the rest.
  - The Godot CS0108 warning (`BuildingRenderer.Scale`) is still there.

## M8-T5 — Godot: block build tool, plan view, material totals (2026-09-27)
- Done: VIEW-21..23 and the CON-01 colours in the view.
  - ViewCore `BlockTool`: shape modes (Tab), height from the slice or +/- / Ctrl + wheel, plan mode (P), a ghost
    coloured by material with invalid cells red and a reason, a tooltip, and no command when no cell is valid.
  - `ToolController`: Blocks (K) and Release (L). Release and Deconstruct take column-box drags up to the slice.
    `ReleaseAll`.
  - `PlanGhostMesher`: translucent plan ghosts, lighter for Planned and red when stuck, plus hover status text.
  - `BlockGhostMesher` draws the tool ghost.
  - `TopBarModel.PlanText`: "Building: … · Planned: …" with short items flagged.
  - Godot: `GameRoot.Blocks.cs`, `PlanRenderer`, `TranslucentMesh`, the HUD block picker and options row (bottom
    left), and a second top-bar row.
  - Sim: batched `BlockPlans.CanPlanAll` and `Statuses` (read-only, about 35 lines).
  - The CS0108 warning is fixed (`BuildingRenderer.Scale` was renamed to `LabelScale`).
- Tests: the 6 M8-T5 placeholders moved to `View/BlockToolTests` with bodies, plus 2 new tests (tool ghost mesh and
  the `blocks` script on seed 1). They first failed to compile (the API was missing).
  `ChunkMesher_ConstructionBlocksUsePaletteColours` would have passed before, because the mesher already used the
  palette. check.sh: 589 passed, 4 skipped (M8-T6), 0 failed. The Godot build has 0 warnings.
- Decisions: ADR-065.
- sim-reviewer: not run (Aurvangar.Sim change about 35 lines, read-only queries).
- Golden: unchanged. Headless `--seed 1 --script survival --ticks 24000`: hash `4caa8837b195f900` (unchanged),
  5,192 ticks/s.
- Perf: n/a (no water, path or tick-loop change); perf.sh not run.
- Screenshots (`SCRIPT=blocks`):
  - `artifacts/screens/m8t5_700/blocks.png` (tick 700): the planks wall is being built. Released Stone ghosts and the
    planned Polished stone box ghosts are visible, with the tool ghost and tooltip. The top bar reads "Building: log
    12/30, stone 18/0 · Planned: stone 128", with stone in orange.
  - `artifacts/screens/m8t5_2500/blocks.png` (tick 2500): the planks are built, and the tool drag across them reads
    "Something solid is there (2 cells skipped)".
  - The Stone wall stays unbuilt (NoMaterial), because the 2-deep pit yields no stone (GEN-04). The red cells of the
    tool ghost are faint at this camera distance.
- Next: M8-T6 (monument scenario). Notes:
  - `MonumentScript` can follow `BlocksScript` and `ScreenshotScripts` (`PitDig`, `PitBox`, the `Find` site search
    with `CanPlanAll`). `ScreenshotScripts.Names` now includes "blocks"; add "monument" the same way.
  - A stone monument needs a quarry deeper than 5 cells (dirt fills h-4..h-1), or it will sit at NoMaterial.
  - `BlockPlans.Statuses` is the cheap way to count entries by status in scenario asserts.

## M8-T6 — Monument scenario (2026-09-27)
- Done:
  - `MonumentScript` (ViewCore): quarry into the hill, two warehouses in the quarry room, a hollow 7×7, 8-high Masonry
    tower with a 2-high door and an inner stair, a 2-high courtyard wall with a gate; the plan is released course by
    course from tick 9600, driven by plan state.
  - `ScreenshotScripts` and `ScreenshotPresets` register "monument" (timed); `run-headless.sh --script monument`.
  - Sim (about 40 lines): the pump buffer Haul runs at the OperatePump priority (BLD-14); Build stand cells prefer
    only cells a living dwarf can reach (CON-11 bug fix: unreachable preferred stands caused give-ups);
    `TickPhase.BlockBuild` profiler phase.
  - Specs: CON-11, BLD-14, JOB-05 note; docs/testing.md (script, preset, CON-P1).
- Tests: the 4 M8-T6 placeholders moved to `Scenarios/MonumentScenarioTests` with bodies (complete by day 10 with all
  alive; no dwarf walled in and no floating block every 100 ticks; save/load at 12,000 identical every 100 ticks for
  2,000; script registered and harness hash at 3000 equals `MonumentScript.Run`). `PendingAcceptanceTests.cs` was
  empty and is deleted. New: `Perf/MonumentPerfTests.SimP1_DuringMonumentBuild`, `PumpTests.BufferHaul_OutranksDigAndBuild`,
  `BlockBuildScenarioTests.PreferredStand_OnlyWhereADwarfCanReach`. check.sh: 595 passed, 0 skipped, 0 failed.
- Decisions: ADR-066.
- sim-reviewer: not run (Aurvangar.Sim change about 40 lines).
- Golden: regenerated: the pump buffer Haul priority (20 → 40) changes the survival session. Tick 3000
  `8283dd6fdae77ad6` → `900c5586280f3a34`, tick 6000 `8fdcd9964d3947f1` → `37a2ae88bb18d81f`; ticks 0 and 1200
  unchanged. Headless survival 24,000 ticks: all 5 alive, 0 failed jobs, hash `e1ddb97096eb0267`.
- Perf: perf.sh all 9 passed. CON-P1 (ticks 13,000-13,499): median 0.085 ms, p95 1.03 ms, max 3.16 ms; blocks 97 →
  113 of 221; Build poster 49.1 ms and regions 36 ms over the 500 ticks; place trial 677 exact checks, 6 cut
  computes. SIM-P1 median 2.34 ms.
- Monument: complete at tick 18,316 (day 7.6), 221 blocks, all 5 alive, no rejected commands, 227 stone stored by
  tick 9600. Headless `--script monument --ticks 24000`: 1 failed job, hash `f4e783f7bf7e8725`, 3,916 ticks/s.
- Screenshots (`SCRIPT=monument SHOTS=monument`): 11000 shows the first courses and plan ghosts, with some dwarves
  idle waiting for the next course; 15000 the tower half built; 18600 the finished tower, stair top and courtyard
  gate. The quarry is inside the hill and not visible. HUD issue: when the plan line is long, the top bar overlaps
  the toolbar ("Day 7" over "Cancel (Z)"); this predates M8-T6.
- Next: M8-GATE (HUMAN-GATE G4). Show the monument shots (TICKS 11000/15000/18600). Known weak spots for the gate:
  idle dwarves between courses, small Build batches when a whole plan is released at once (ADR-066), the HUD
  overlap above.

## M8-GATE — HUMAN-GATE G4: build a monument (2026-09-27)

**Status: waiting for human review. The gate box in BACKLOG.md is NOT checked; check it after review.**
All M7 and M8 tasks are checked. The backlog has nothing after this gate. No code changed for this report.

### Verdict in one paragraph
Free-form construction works end to end on seed 1. The player can paint blocks in six shapes, keep them as a plan,
see the stone needed against the stone stored, and release the plan in parts. Dwarves quarry stone, haul it and
place it bottom-up. They never float a block or wall a dwarf in. The scripted monument is a hollow 7×7, 8-high
Masonry tower with a door and an inner stair, plus a walled courtyard with a gate (221 blocks). It is complete at
tick 18,316 (day 7.6) with all 5 dwarves alive. It is deterministic, including a save/load mid-build. Every perf
budget passes with at least 3x headroom. The weak points are pacing and feel. When a whole plan is released at
once, dwarves carry about one block per trip. The script releases one course at a time, and then dwarves stand
idle between courses. On seed 1 the player must quarry 5 levels down before building in stone. **Nobody has built
anything by hand yet.** That is the main question of this gate.

### Build and test results (this run, HEAD = M8-T6 `8132490`)
- `./scripts/check.sh`: **OK**. 595 passed, 0 skipped, 0 failed (non-Perf, Debug). Solution and Godot csproj both
  build with 0 warnings.
- `./scripts/perf.sh`: **OK**. 9 of 9 passed (Release). SAV-05 (save size) runs in check.sh and passed.

| ID | What | Budget | Measured | Use |
|---|---|---|---|---|
| WAT-P1 | water step, 192×128 sustained (≥ 23,048 active) | ≤ 4 ms | median 1.26 ms, p95 1.31 | 32% |
| WAT-P1 | water step, 128×128 (15,494 active) | ≤ 4 ms | median 0.87 ms, p95 0.92 | 22% |
| WAT-P2 | seed-1 settled river, active cells at tick 1200 | ≤ 3,000 | 699 (step median 0.031 ms) | 23% |
| PTH-P1 | A*, 200 paths of 90..110 cells | p95 ≤ 1.5 ms | median 0.242, p95 0.613, max 0.796 ms | 41% |
| PTH-P2 | region rebuild, seed 1 | ≤ 25 ms | warm 2.11 ms, cold 2.42 ms | 10% |
| ECO-16 | moisture recompute | ≤ 3 ms | median 0.873 ms | 29% |
| MESH-P1 | chunk (3,0,2), 581 quads | ≤ 6 ms | median 0.629 ms, p95 0.695 (slice 0.541) | 10% |
| SIM-P1 | full tick, survival script, ticks 12,000-12,499 (drought) | median ≤ 8 ms | median 2.386, p95 2.620, max 3.57 ms | 30% |
| CON-P1 | full tick, monument script, ticks 13,000-13,499 | median ≤ 8 ms | median 0.087, p95 1.100, max 3.42 ms | 1% |
| SAV-05 | save at day 5 | ≤ 3 MB | 31,781 bytes (M6-T7) | 1% |

Nothing is within 20% of its limit. CON-P1 covers blocks 97 → 113 of 221. Over its 500 ticks, the Build poster
takes 50.1 ms, regions take 37 ms and water takes 0 ms. The place trial makes 677 exact checks and 6 cut computes.
SIM-P1: region rebuilds take 922 ms of the 500 ticks (409 rebuilds). That is still the largest share of a drought
tick, as at G3.

### Headless runs (this run)
- `./scripts/run-headless.sh --seed 1 --script monument --ticks 24000`: hash **`f4e783f7bf7e8725`** (same as
  M8-T6), 3,931 ticks/s, tick median 0.067 ms, p95 2.21 ms. **5/5 alive at day 10**. 688 jobs done, **1 failed**.
  250 of 250 cells dug (corridor and quarry), 7 of 7 trees felled. 0 items on the ground. 1,272 path searches,
  1,318 region rebuilds, 0 trapped. Stored at day 10: log 6, stone 6, berries 65, water 110. The monument itself is
  complete at tick 18,316 (from M8-T6, and `MonumentScenarioTests` asserts it by day 10 in this check.sh run).
  **The pump is dry on 5,015 ticks (4,704 in the drought).** The monument script has no reservoir, so the colony
  lives on stored water through the drought (see `g4_15000`).
- `./scripts/run-headless.sh --seed 1 --script survival --ticks 24000`: hash **`e1ddb97096eb0267`** (same as
  M8-T6), 5,338 ticks/s, tick median 0.041 ms, p95 0.97 ms. **5/5 alive at day 10**. 444 jobs done, **0 failed**.
  73 of 73 cells dug, 23 of 23 trees felled. 60 crops harvested, **0 withered** (the G3 run had 41 withered).
  **The pump is dry on 855 ticks, 0 in the drought** (G3: 4,712 in the drought). The levee reservoir (M7-T3) works.
  Stored at day 10: log 74, stone 22, berries 65, potato 160, water 110.

### Screenshots (Godot 4.6.2 .NET, Forward+, seed 1; rendered for this gate and looked at)
- `artifacts/screens/g4_11000/monument.png` (`SCRIPT=monument SHOTS=monument TICKS=11000`, day 5). The first two
  courses of the tower are up in gray Masonry, with the 2-high door gap. The courtyard wall is 2 high with its
  gate. The rest of the tower is a stack of white translucent plan ghosts. The top bar reads "Building: stone
  2/155 · Planned: stone 149" (2 stone needed by released cells, 155 stored). **3 of 5 dwarves are Idle**, waiting
  for the next course to be released. The other 2 are "Placing blocks".
- `artifacts/screens/g4_15000/monument.png` (day 7, drought). The tower is about 5 of 8 courses high, with ghosts
  above it. All 5 dwarves are "Placing blocks". The top bar reads "Building: stone 25/80 · Planned: stone 49" and
  "Pump has no water" in red. **The top bar overlaps the toolbar: "Day 7" is drawn over "Cancel (Z)".** One faint
  red ghost cell (a stuck plan entry, shown red per VIEW-22) is at the top right of the tower and is hard to see.
- `artifacts/screens/g4_18600/monument.png` (day 8). The tower is finished: a hollow 8-high box, with the top of
  the inner stair visible as a notch in the far inside corner, and the door and courtyard gate at the front. No
  ghosts remain, and the plan line is gone from the top bar. Stone 6 is left. The dwarves are back to harvesting
  and drinking.
- `artifacts/screens/g4_700/blocks.png` (`SCRIPT=blocks SHOTS=blocks TICKS=700`, day 1). This shows the block tool.
  The Stone-wall tool is active with the options row at the bottom left: "Stone wall · Single · Line · **Wall** ·
  Floor · Box · Stair · Plan (P) · Height 3 (+/-) · Release all". The tool ghost has the tooltip "Build Stone wall:
  Wall, height 3 (12 blocks, 12 stone) / Tab shape, +/- height, P plan, Shift keeps the tool". A planks wall is
  being built (released ghosts), and a planned Polished-stone box is visible. The top bar reads "Building: log
  12/30, stone 18/0 · Planned: stone 128", with the short stone in orange.
- The quarry is inside the hill and does not show in any preset.

### What was built since G3
- **M7 playability pass (G3 answers):**
  - M7-T1: right-drag orbits like middle-drag, and a right click still cancels.
  - M7-T2: a pump may use the stand cell one level up, so there is no hand-dug notch on seed 1.
  - M7-T3: player-held water keeps fields moist, and the survival session builds a real levee reservoir.
  - M7-T4: dwarves eat the most plentiful food first.
  - M7-T5: a give-up mark and HUD notice for recurring jobs that can never succeed.
  - M7-T6: no dig strands any dwarf.
  - M7-T7: readable labels and timed screenshot scripts.
- **M8 free-form construction:**
  - Three construction blocks: Masonry (1 stone), Planks (1 log) and Polished stone (2 stone).
  - Six shapes: Single, Line, Wall, Floor, hollow Box and Stair.
  - A plan layer, with Planned and Released entries and a material total.
  - Rules: support (no floating blocks), bottom-up build order, and no walling a dwarf in.
  - Batched build jobs from storage.
  - Deconstruction with a full refund, which never ungrounds a block.
  - The Godot block tool, plan ghosts and top-bar totals.
  - The MonumentScript.

### What is weak (open issues, with evidence)
1. **Right-drag.** At G3 you said "I find I am right-clicking to pan". M7-T1 made right-drag *orbit* (turn the
   camera), the same as middle-drag. If "pan" meant sliding the map sideways, the binding is wrong (see Q2).
2. **Small build batches** (ADR-066). A job is posted as soon as one cell is Ready. When a whole plan is released
   at once, cells become Ready one at a time as the courses below finish. Batches then average about 1.3 blocks,
   so a dwarf walks to the warehouse for nearly every block. The monument script works around this by releasing
   course by course. A player pressing "Release all" gets the slow behaviour.
3. **Idle dwarves between courses.** With a course-by-course release, 3 of 5 dwarves stand idle while the last
   cells of a course finish (`g4_11000`). Issues 2 and 3 have one root cause: the poster does not gather work
   ahead of the build front.
4. **Top-bar overlap.** A long plan line pushes the top bar left over the toolbar (`g4_15000`: "Day 7" over
   "Cancel (Z)").
5. **Faint red cells.** Invalid tool-ghost cells and stuck plan cells are a faint red at normal camera distance
   (`g4_15000`, and the M8-T5 shots).
6. **Reach.** Dwarves reach only 1 level up from where they stand, so anything taller needs a stair (CON-11). The
   tower has two inner stair flights for that reason. A free-standing Stair only keeps its bottom step, so stairs
   must lean against a wall (M8-T2 note).
7. **Floating piles.** A refund pile can be left in mid-air when the block under it is dug later, as with natural
   digs. It is still hauled away.
8. **Quarry depth.** On seed 1, dirt fills the top 4 levels, so stone lies 5 or more levels deep. A player must
   dig a quarry before building in stone (the monument script digs 250 cells for 221 blocks). The M8-T5 `blocks`
   shot shows a Stone wall stuck at NoMaterial after a 2-deep pit. Logs (Planks) are the only material at hand.
9. **Support check caps.** The plan-support flood stops at 16,384 cells and the removal check at 4,096 cells per
   neighbour (`Support.PlanFloodCap`, `Support.DependsCap`). Above a cap the answer is "unsupported" or "depends",
   so a very large connected structure would be rejected or never come down. One shape is capped at 4,096 cells
   (`BuildShapes.MaxCells`). Nothing in play has come near these limits.
10. **Monument pump dry in the drought.** The monument script has no reservoir. Its pump is dry on 4,704 drought
    ticks, and the colony lives on stored water (fine for 2 days).
11. **Material variety.** There are three block types and two raw materials. There is no colour choice, no
    glass, no roofs or slopes, and no decoration.
12. **Not played by hand.** Every M8 rule is proven by tests and a script. The block tool has only been seen in
    screenshots.

### ADRs made since G3 (13)
054 right-drag orbit, 055 pump stand one level up, 056 survival reservoir, 057 most plentiful food, 058 give-up
marks, 059 strand rule for every dwarf, 060 readable labels and timed screenshot scripts, 061 free-form
construction (amends ADR-001), 062 build-block jobs, 063 deconstructing built blocks, 064 plan layer, 065 block
tool / plan view / totals, 066 monument session (buffer haul priority, reachable stand cells, course-by-course
release).

### How to play
Launch the game (PowerShell):

```powershell
& "C:\tools\godot\Godot_v4.6.2-stable_mono_win64.exe" --path "E:\ai\aurvangar\src\Aurvangar.Godot"
```

Block building controls (updated by M9-T1, ADR-067: single blocks, no shapes or height):
- **K**: block tool. Pick the material (Stone wall, Wood planks, Polished stone) from the Blocks menu.
- **Click** a block face: one block in the cell that face looks into. A top face stacks up; a side face places
  beside.
- **Drag**: paint one block into each cell the cursor passes, on the first cell's layer. Build a wall course by
  course. The tool stays active; **Esc** leaves it, and a right click drops the drag.
- **P**: toggle plan mode. Planned blocks are not built until they are released.
- **L**: release a box of the plan. **Release all** (button) releases the whole plan.
- **X**: deconstruct, click or drag per built block, with a full refund. A click on a building tears it down.
  **Z**: cancel.
- **G**: dig, which you need for a stone quarry (stone is 5 or more levels down on seed 1).
- Camera: right-drag or middle-drag orbits, and a right click cancels the current drag.

### Questions for the human
1. **Build something by hand**, such as a wall, a tower or a planned monument, and tell me how it felt. Useful
   points: was the block tool easy to aim? Were the shapes and height controls clear? Did plan, then release, feel
   like planning a monument? Was waiting on the dwarves fun or tedious? Was quarrying stone first a chore?
2. **Right-drag: orbit or slide?** Right-drag now orbits, like middle-drag. When you said "pan", did you mean
   sliding the map sideways (moving the camera's focus point) instead? If so, right-drag should slide, and
   middle-drag should keep orbiting.
3. **Next milestone.** My recommendation is a short construction follow-up first:
   - Build batching: gather Ready cells ahead of the build front, so "Release all" gives full batches and no idle
     dwarves (issues 2 and 3).
   - The top-bar overlap and stronger red cells (issues 4 and 5).
   - More materials, and possibly easier stone on the surface (issue 8).

   Should the next milestone do that, or move on to something else (scale and robustness, or the long-term
   direction in docs/00-overview.md)?
4. **Anything else** from your play session: bugs, confusions, or things you wanted to build and could not.

Next after approval: whatever tasks the answers add. The backlog is otherwise empty.

### G4 answers (human, 2026-09-27)

- **Right-drag:** "right click to drag is great." Right-drag orbit (M7-T1) stays as it is.
- **Building:** "I think building should be one square at a time." Asked what that meant, the human chose
  **the player places single blocks**. The shape modes are dropped from the player's tool, and the sim keeps them
  for scripts. -> M9-T1.
- **Next milestone:** the human took the recommendation, construction polish:
  - single-block tool -> M9-T1;
  - build batching -> M9-T2;
  - top-bar overlap and clearer red cells -> M9-T3;
  - more materials -> M9-T4;
  - then gate G5.

## M9-T1 — Block tool places single blocks (2026-09-27)
- Done: G4 answer "building should be one square at a time". The player's block tool paints single blocks.
  - ViewCore `PaintDrag`: a drag on one layer. The mouse ray is cut with the first face's horizontal plane (the
    hovered pick is the fallback), and cursor cells are joined by a 4-connected `Line4`. Each cell is painted once.
    Limits: 1,024 cells, and jumps over 64 cells are ignored.
  - `BlockTool` was rewritten. A click places into `PickHit.Adjacent`: a top face stacks up, a side face places
    beside. A drag paints. The ghost shows the painted cells, with one `CanPlanAll` over them. The release sends one
    `DesignateBuild(Single)` per valid cell in `SupportOrder`. The tool stays active. Shapes, Tab, height, +/- and
    Ctrl + wheel are removed from the player's tool; the sim shapes stay for scripts and tests.
  - `DeconstructPaint`: click or drag per built block on the first block's layer, one
    `DesignateDeconstructBlocks(c, c)` per block, with orange marks. The Deconstruct column box in
    `ToolController` is gone (`BeginDrag` removed). A click on a building is still BLD-09.
  - Godot: `GameRoot.Blocks.cs` (paint input through the mouse ray, deconstruct marks through `BlockGhostMesher.Marks`,
    `ShowBlockPaint` for the harness). The HUD row now reads "block · click a face for one block, drag to paint a
    course · Plan (P) · Release all". The camera zooms on Ctrl + wheel again.
  - New timed screenshot script and preset `paint` (`PaintScript`). It uses the tool itself to paint a released
    planks L (10 blocks) at tick 0 and a planned second course (7) at tick 1. The harness holds a stone paint drag.
    `run-headless.sh --script paint` works too.
  - Docs: VIEW-21 rewritten, docs/testing.md (the paint script and preset), and the M8-GATE control list in this
    file.
- Tests: new `View/BlockPaintTests` (7 tests):
  - a click places one block, a top face stacks, a side face places beside;
  - `Line4` is face-connected;
  - a drag paints each cell once on the first layer (by pick, by ray, with the parallel-ray fallback and the jump
    limit), and the painted plan lands in the sim;
  - the paint plane follows the picked face;
  - invalid cells are skipped, and support order lets an overhang painted from its free end be accepted (paint
    order is rejected by the sim);
  - deconstruct by block (click and drag);
  - the paint script on seed 1.

  They were written against the new API, so they did not compile before it existed. In `BlockToolTests`, the two
  shape/height tests and the Deconstruct column-box asserts are replaced (the human removed that behaviour;
  ADR-067). check.sh: 600 passed, 0 skipped, 0 failed. The Godot build has 0 warnings.
- Decisions: ADR-067 (amends ADR-065).
- sim-reviewer: not run (no Aurvangar.Sim change).
- Golden: unchanged (the sim is not touched). Headless `--seed 1 --script paint --ticks 1500`: 10 logs used, hash
  `6b3f03de58769a01`.
- Perf: n/a (no sim, water or path change). perf.sh was not run.
- Screenshots (Forward+, seed 1, looked at):
  - `artifacts/screens/m9t1_3/paint.png` (`SCRIPT=paint SHOTS=paint TICKS=3`): the painted L as released planks
    ghosts, the planned second course above it, and the held Stone wall drag as a staircase of single cells. The
    tooltip reads "Build Stone wall (11 blocks, 11 stone) / Click a face: one block. Drag: paint a course. P plan".
    The top bar reads "Building: log 10/30 · Planned: log 7".
  - `artifacts/screens/m9t1_1500/paint.png` (TICKS=1500): the planks L is built from single blocks, and the planned
    course ghosts sit on its long arm. The held stone drag has one red cell where it crosses the built L ("Something
    solid is there"). The mouse label covers part of the L's short arm.
  - `artifacts/screens/m9t1_2500/blocks.png` (`SCRIPT=blocks`): the harness drag is now a 4-cell paint across the
    planks wall. The red cell is still faint at this distance (M9-T3).
- Next: M9-T2 (build batching) has no dependency on this task. M9-T3 needs this task: the HUD block row is now
  shorter (no shape buttons). Red ghost cells are still faint. M9-T4: the Blocks menu lists
  `BlockTool.Blocks`, which are all `IsConstruction` blocks in id order, so new blocks appear there automatically.
  The G5 gate should use `SCRIPT=paint` for the "single-block painting" shot.
