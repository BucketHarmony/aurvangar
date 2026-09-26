# Decisions (ADR log)

Short records of choices made during the build. Newest at the bottom. Claude Code adds an entry whenever a spec is
ambiguous or wrong, a budget forces a design change, or a hard rule needs an exception.

Template:

```
## ADR-NNN: <title> (<date>, <task id>)
Context: <what forced the decision>
Decision: <what we do>
Consequences: <what this makes easier/harder; follow-ups>
```

---

## ADR-001: Timberborn-style prefab construction, indirect control (2026-09-25, scaffold)
Context: The references split between direct-control (Minecraft) and indirect-control colony sims.
Decision: Indirect control only. Buildings are prefabs placed as blueprints; terrain is shaped by dig designations
(DF style) and levees. Players and colonists share the WorldActions API so a player avatar can be added later as an
Agent driven by input.
Consequences: No avatar, no freeform block placement except levees in the POC.

## ADR-002: Godot 4.6 .NET, C# only, sim as a Godot-free library (2026-09-25, scaffold)
Context: Carried over from the project's earlier decisions. Godot 4.7 exists; 4.6 is kept for stability of the
decision record. GodotSharp 4.6.x targets net8.0.
Decision: Godot.NET.Sdk/4.6.2, net8.0 everywhere, Aurvangar.Sim and Aurvangar.ViewCore never reference Godot.
Consequences: Upgrading to 4.7 is a one-line csproj change plus project.godot features; do it only via a new ADR.

## ADR-003: Game data embedded in Aurvangar.Sim (2026-09-25, scaffold)
Context: Sim, tests, headless tool and Godot all need the same data without path logic.
Decision: data/*.json are EmbeddedResources of Aurvangar.Sim, loaded by ContentDb.LoadEmbedded().
Consequences: Editing data requires a rebuild. Acceptable for the POC; modding is out of scope.

## ADR-004: Integer-only sim state and fixed-point water (2026-09-25, scaffold)
Context: Determinism across runs and machines is required for golden tests, save/load equality and replay.
Decision: No float/double in Aurvangar.Sim (enforced by scripts/sim-guard.sh). Water in 1/1024 cell units.
Trigonometry from a literal integer table (Core/Fixed).
Consequences: Slightly more verbose math. Palette floats live in Content/Defs.cs, which is exempt because the sim
never reads them.

## ADR-005: Thirst and a pump added to the MVP (2026-09-25, scaffold)
Context: The earlier scope had hunger only, with water mattering only through farm moisture.
Decision: Add thirst and the Water Pump. Water then matters directly, and the drought creates the core Timberborn
tension (store water or build a reservoir).
Consequences: One more need and one more building. Both are small.

## ADR-006: Godot C# namespace is Aurvangar.Client (2026-09-25, scaffold)
Context: A namespace named Aurvangar.Godot would shadow the Godot namespace inside the project.
Decision: The folder and csproj are Aurvangar.Godot; the root namespace is Aurvangar.Client.
Consequences: None beyond naming.

## ADR-007: Godot project is not in Aurvangar.sln (2026-09-25, scaffold)
Context: Godot.NET.Sdk must resolve from NuGet for any tool that loads the solution; the sim, tests and tools do not
need it.
Decision: Aurvangar.sln contains Sim, ViewCore, Tests, Headless. check.sh builds the Godot csproj separately.
Consequences: IDEs should open Aurvangar.sln for sim work and the Godot project folder for view work.

## ADR-008: Name and theme: Aurvangar, dwarves (2026-09-26, scaffold)
Context: The project needed a name before its namespaces spread. The owner chose a dwarf theme and a Norse source.
Decision: Title, repo, solution and namespaces are Aurvangar (`Aurvangar.Sim`, `Aurvangar.ViewCore`,
`Aurvangar.Client`). Colonists are dwarves in all player-facing text; code identifiers stay generic. The long-term
endgame (science, exploration, diplomacy) is recorded in docs/00-overview.md as direction only.
Consequences: Player-facing strings (UI labels, names) use dwarf flavor from M4-T11 onward. No Tolkien-derived words.

## ADR-009: River channel geometry and "river distance" (2026-09-26, M1-T3)
Context: GEN-03 says "bed at baseHeight - 4" and "banks slope 1 block per cell for 2 cells", and GEN-04/06/07
measure distances "from the river" without defining them. A bed that follows the local noise height would make
pools and dams along the channel; banks that stop after 2 cells leave a 2+ block cliff (terrain sits ≥ 4 above the
bed), so colonists could not walk down to the water (pump, DoD step 2).
Decision: `baseHeight` is GEN-01's minimum base height (18). The channel (|z - center(x)| ≤ 3) has a constant floor:
the lowest water cell is y = 14 ("bed"), the solid floor top is y = 13; GEN-08 sources and the initial fill cover
y = 14..17 ("bed .. bed+3"). Banks slope up 1 block per cell from the channel edge until they meet natural terrain.
"Distance from the river" is `max(0, |z - center(x)| - 3)` (cells from the channel edge along Z): Sand where ≤ 2,
no trees where ≤ 3, bushes where 4..12. Bushes' "within 30 cells of the hub" is Chebyshev on X/Z from the spawn
flat center. Drains cover the channel z range at x = 127 for y = bed .. top of the world. The hub is centered on
the flat using its footprint from data (`TerrainResult.HubOrigin(footprint)`; its entrance faces north onto the flat).
Consequences: The river is fed/drained by level difference only (flat bed); the M2-T5 settle and WAT-P2 budget are
measured on this geometry. Initial water also fills the lower bank cells (y ≤ 17), so the wet surface is wider
than the 7-cell channel.

## ADR-010: Water active set holds wet cells only and is hashed state (2026-09-26, M2-T1)
Context: WAT-02 says a cell becomes active when it or a neighbor changes, but every step rule (WAT-04..07) applies
only to wet cells, and activating dry air/solid neighbors would inflate `ActiveCount` against the WAT-P2 budget.
Separately, which cells step next affects the outcome (a cell outside the set is not re-evaluated), so the set is
state, not a cache.
Decision: `Activate` admits only cells with level > 0 (solid cells always hold 0). A dry cell that receives water
changes level and is activated then. A cell that dries during a step is not re-flagged; one dried by `SetLevel`
while already flagged stays in the set until the next step, which drops it.
`WaterGrid.AddToHash` includes the active set as sorted indices. Save/load (M4-T10) must persist the active set
rather than recomputing it.
Consequences: `ActiveCount` counts wet cells that may change. M2-T4 dig activation (WAT-13) works through the wet
neighbors of the dug cell. Adding the (empty) set to the hash changed the seed-1 golden hashes.

## ADR-011: Spread/film details, sources raise only, and the shaft test's "bottom up" (2026-09-26, M2-T2)
Context: (1) WAT-06 does not say whether minimum flow competes with normal spread flows; WAT-07 does not say which
level it tests or whether it runs before fall. (2) WAT-09 says sources are "set to" `Full * strength / 100`, but
lowering a cell would remove volume that no `WaterStats` counter records, breaking WAT-11. (3)
`WaterScenarioTests.ShaftFillsBottomUp` asserted every tick that each wet shaft cell has a Full cell below it. That
cannot hold with WAT-04 (one cell per step, as `Fall_MovesOneCellPerStep` requires): the first water is over empty
cells while it falls. The double-buffered step (WAT-03) also delivers a source's stream as separate Full slugs,
because the source cell cannot fall into a cell that is Full in the snapshot.
Decision: (1) All rules read the pre-step snapshot. WAT-07 is checked first, on the cell's snapshot level, and
applies only when the floor is solid (so a falling film never evaporates); solid and out-of-world neighbors count
as 0. Minimum flow sends 1 unit to the lowest neighbor whose flow rounded to 0 with a difference of ≥ 2 (ties: lowest
index), even if other neighbors received normal flow that step. Out-of-world horizontal neighbors are walls.
(2) Sources only raise a cell to the target (added volume to `SourceAdded`); they never lower it. Sources are applied
in M2-T2 because `ShaftFillsBottomUp` needs them; drains stay in M2-T3. (3) The shaft test now asserts that no water
ever sits over a partially filled shaft cell, the bottom cell is 0 or Full every tick, and the whole shaft is Full at
the end.
Consequences: Overfill (WAT-16) can occur from spread alone (four min-flow inflows of 1 into a cell 2 below Full);
tests cover both the open-air push and the ceiling evaporation. In Drought (strength 0) sources add nothing and the
river drains through drains and evaporation only.

## ADR-012: Basin scenario source sits above the rim (2026-09-26, M2-T3)
Context: `WaterScenarioTests.BasinFillsThenOverflows` (water.md scenario 2) put its source at (1,7,1), in the top
interior layer of a basin whose walls also end at y=7. WAT-09 sets a source cell to `Full * strength / 100` and
never above, and no rule moves water up except WAT-16 overfill (level > Full). A full basin with the source inside it
therefore stays at exactly Full in every y=7 cell and can never overflow the rim, whatever the implementation. The
alternative, sources that add `Full` per step and overfill upward, contradicts WAT-09's wording and would make
GEN-08's river source column (bed..bed+3) push water up without limit.
Decision: Keep WAT-09 as written (set/raise to target). Move the scenario's corner source one cell above the rim,
to (1,8,1), where it still pours into the corner of the basin and overflows once the basin is full. The fill
and overflow assertions are unchanged.
Consequences: A source only drives water up to its own cell height. Scenarios that expect a source to overflow a
container must place the source at or above the rim.

## ADR-013: Water consumes world changes twice per tick; WaterDirty baselines are not state (2026-09-26, M2-T4)
Context: (1) The water step runs third in ARCH-01, but buildings (7) and agents (10) change blocks later in the
tick, and `Simulation` clears `VoxelWorld.ChangedCells` at the end of each tick. Reading the log only at the start
of `Water.Tick` would miss those changes, and a new solid cell would keep its water until something else touched it.
(2) WAT-12 does not say how shares interact with a neighbor that has less free room than its share. (3) WAT-15 needs
a per-cell "level at the chunk's last event" to measure drift; it only throttles view events.
Decision: (1) `WaterGrid` reads the log through a cursor: `Tick` consumes the entries made before the step (commands,
setup, test edits between ticks) and `EndTick`, called right after `Agents.Tick` and before `PathGrid.Invalidate`
and `ClearChangeLog`, consumes the rest and resets the cursor. Each entry is judged by the cell's current block.
Every changed cell and its 6 neighbors are activated (WAT-13; ADR-010 still admits only wet cells), and a cell that is
now solid with water in it is pushed out (WAT-12). (2) Push: equal shares `L / n` to the open horizontal neighbors,
each capped at `Full - level`; the division remainder plus whatever did not fit goes to the cell above if it is open
and in the world (also capped); the rest is counted in `Evaporated`. (3) WaterDirty fires for the chunk that contains
the changed cell, when that cell's level is ≥ 32 from its baseline or crossed 0 ↔ wet. When a chunk emits, the
baselines of all its cells that changed since its last event are reset. Events are emitted at the end of `Tick` and
of `EndTick`, chunks in ascending order. The baselines are view-notification state: they are not hashed and not
saved (after a load the view remeshes everything anyway, so the loader may start the baselines at the current levels). No sim system may react to WaterDirty; if one
ever needs to, the baselines become state and must be hashed and saved. Nothing may call `SetBlock` after `EndTick` in
a tick. If the log is cleared outside `Simulation.Tick` while the cursor is ahead of it, the cursor restarts at 0.
Consequences: Neighbor chunks are not dirtied by a border cell change; a water side face at a chunk seam can stay stale
until the neighboring chunk gets its own WaterDirty (M3-T3/T4 may revisit if seams show). Water level changes from the push are visible to the
`PathGrid` in the same tick.

## ADR-014: World-creation pre-settle runs the full tick loop, then resets clock, water stats and events (2026-09-26, M2-T5)
Context: GEN-08 says to run the water sim for 600 ticks inside `WorldFactory` and `RiverTests` requires the clock to be
0 afterwards, but does not say how the step is driven or what happens to `WaterStats` and queued events. Calling
`Water.Tick` alone would skip `Water.EndTick` and `ClearChangeLog` (ADR-013 cursor). Keeping the pre-settle's
`SourceAdded`/`Drained`/`Evaporated` would make WAT-11 checks from tick 0 need the pre-fill volume, which no caller has.
Decision: `WorldFactory.PreSettleRiver` registers the terrain's sources and drains, sets every `InitialWater` cell to
`Full`, calls `Simulation.RunTicks(600)`, then sets `Clock.Tick = 0`, calls `WaterGrid.ResetStats()` and drains the
event bus. The active set and levels are kept (they are the settled state). No other system acts during the settle yet;
M4-T4 spawns colonists after it, so agents never run during pre-settle.
Consequences: Conservation is measured from the tick-0 total. The view gets no WaterDirty backlog from world creation;
it builds everything from `MarkAllDirty`. Systems added later that act in `Tick` (plants growth, weather) will also run
for the 600 settle ticks unless they are registered after `PreSettleRiver`; keep colonists, stock and anything
time-dependent after it.

## ADR-015: Water step uses a radix index sort and skips tier-0 JIT; WAT-P1 measured at a sustained 20k+ load (2026-09-26, M2-T6)
Context: With the 128x128 test in `WaterPerfTests` passing at ~2.5 ms, a load that stays at 20,000 active cells
(WAT-P1) measured 3.1-4.0 ms median, within 20% of the 4 ms budget. Profiling showed about 1 ms per step in the two
`List<int>.Sort` calls (the active set, WAT-03, and the touched-cell list in `ApplyDeltas`). The rest of the cost was
tier-0 JIT code, because a perf test lasts only about 150 ms.
Decision: Add `Core/IndexSort`, an LSD radix sort with 11-bit digits for non-negative indices. Lists shorter than
256 still use `List.Sort`. The output is the same ascending order, so hashes and golden files do not change.
`WaterGrid.Tick`, `ComputeFlows`, `ApplyDeltas` and `ActivateAround` are marked
`[MethodImpl(AggressiveOptimization)]`. Add `Step_Sustained20kActiveCells_Under4ms`, which uses a 192x128 layer
(world sizes must be multiples of 32; 160x128 drops to 19,306 active cells). It asserts that at least 20,000 cells
stay active for every measured step, so it tests a heavier load than the spec (about 23k cells).
Consequences: Other hot paths (A*, regions, mesher) can reuse `IndexSort`. Budgets are unchanged.

## ADR-016: A slice top face is "cut" only where the real cell above is solid (2026-09-26, M3-T2)
Context: VIEW-04 says to emit top faces of solid cells at `y == SliceY` with the darkened cut color. Read literally,
every exposed surface that happens to lie at the slice level (open ground, a floor under open sky) would be drawn
dark, although nothing was cut there. The scaffold's `ChunkMesher` doc comment already says the cell above must be
solid in the real world.
Decision: A top face of a solid cell at `y == SliceY` is a cut face only if the real world cell at `y == SliceY + 1`
is solid (WLD-04 out-of-bounds rules, so the top of the world is never cut). Cut faces use `BlockColors.GetCut`,
count in `MeshData.CutQuadCount`, and carry a flag bit (`1 << 8`) in the greedy mask key, so cut and uncut faces
never merge. A block whose above cell is in the next chunk is checked through the world, not the padded copy.
Consequences: At the default slice (`SizeY - 1`) there are no cut faces. Cut faces are only emitted by the chunk
that holds `SliceY`, so the VIEW-04 remesh rule (chunks whose Y range contains the old or new slice) still covers a
slice change (M3-T5). A block change at `SliceY + 1` in the chunk above already dirties the chunk below (WLD-03
neighbor dirtying), so cut faces stay current.
