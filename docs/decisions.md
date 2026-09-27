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
Consequences: No avatar, no freeform block placement except levees in the POC. (Amended by ADR-061: M8 adds free-form
block construction; prefabs stay for functional buildings.)

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

## ADR-017: Water mesher emits side faces for every wet cell and tints by column depth (2026-09-26, M3-T3)
Context: The water.md rendering contract says to build top faces for surface cells (wet, dry above) "plus side faces
where a neighbor's surface is lower", and to depth-tint by `level`. Emitting sides only for surface cells leaves
gaps in falls and free-standing columns (only the top cell of a column would have sides), and tinting by the top
cell's `level` alone makes a deep lake look as pale as a puddle. The contract also does not say how slicing
(VIEW-04) and the palette (`water.shallow/deep/alpha`) apply.
Decision: The water top of a wet cell is `y + level/Full` if it is a surface cell, otherwise `y + 1`. Every wet cell
(not only surface cells) emits a side quad toward each non-solid horizontal neighbor whose water top in that cell
is lower (dry = `y`), spanning the difference. Surface cells emit one top quad each (no greedy merge). Cells above
`sliceY` are ignored and count as dry, so a wet cell at `sliceY` under more water becomes a surface cell. Color
lerps from `water.shallow` to `water.deep` by column depth (this cell's level plus `Full` per contiguous wet cell
below, full dark at 3 cells), alpha `water.alpha`, via new `WaterColors`. `WaterMesher.Build` takes an optional
`WaterColors` (default: the embedded palette), so the scaffold signature still works.
Consequences: A lake bed step inside a body of water emits no faces (both sides are `y + 1`). Neighbors in other
chunks are read from the world, so a chunk's water mesh depends on the one-cell border: M3-T4 must also remesh the
water of a neighbor chunk when a border cell's water changes (M2-T4 note: WaterDirty is per chunk of the changed
cell only).

## ADR-018: View loop logic lives in ViewCore; WaterDirty remeshes the chunk and its 6 face neighbors (2026-09-26, M3-T4)
Context: Godot cannot be run on the build machine, so the Godot layer can only be build-verified. WaterDirty
(WAT-15) names only the chunk of the changed cell, but a chunk's water mesh reads its one-cell border (ADR-017).
VIEW-03 asks for picking collision per chunk; VIEW-02 does not say how queues dedupe or order.
Decision: The fixed-step clock (`ViewCore.Frame.TickAccumulator`), deduping FIFO remesh queues (`RemeshQueue`), the
event router (`RemeshRouter`) and the CCW-to-CW winding flip (`Meshing.MeshWinding`) are engine-neutral and
unit-tested; `GameRoot`, `ChunkRenderer`, `WaterRenderer` and `MeshConvert` only copy arrays into Godot nodes.
`ChunkDirty` queues terrain and water of that chunk (a block change can add or remove water side faces; the world
already dirties neighbor chunks). `WaterDirty` queues the water of the chunk and its 6 face neighbors (clipped to the
world). Queues are FIFO, one entry per chunk; the world-creation events are discarded after `EnqueueAll`.
`ChunkRenderer` builds the per-chunk `ConcavePolygonShape3D` (physics layer 1) now, used by M3-T5 picking.
Consequences: Up to 7x more water remeshes than dirty chunks; the per-frame budget (4) bounds the cost, a busy river
can lag a few frames behind. If that shows, WaterDirty could carry a border flag (sim change) instead.

## ADR-019: Camera focus height, phase timing hook, picking and slice-key details (2026-09-26, M3-T5)
Context: VIEW-06 says "camera focus follows slice level height" without saying what happens when nothing is sliced.
VIEW-17 asks for water step ms and region rebuild ms, but the sim must never read wall-clock time, and the view only
sees `Simulation.Tick()` as a whole. VIEW-05 does not say what an ignored pick above the slice does, and VIEW-06 does
not define Q/E direction or what middle-drag does to yaw. Godot cannot be run on the build machine.
Decision: `ViewCore.Camera.OrbitRig` holds the camera math. Its focus height eases (rate 8/s) toward
`min(SliceY + 1, BaseFocusY)`, where `BaseFocusY` is the hub origin height (spawn flat surface), so the focus drops
onto the slice when slicing below the colony and stays at the colony otherwise. Start: focus on the hub, yaw 45,
pitch 45, distance 60. Q/E turn the yaw target by -90/+90 and yaw moves toward it linearly (90 degrees per 0.2 s).
Middle-drag turns yaw freely (and resets the target) and changes pitch, clamped 25-80. Wheel zooms by x0.9 per step,
clamped 10-120. Pan speed is `distance` cells/s, focus clamped to the world XZ box. The sim gets an optional
`ITickProfiler` (`Simulation.Profiler`, phases `Water` and `Regions`): the sim only calls `Begin`/`End`, the view's
`ViewCore.Diagnostics.PhaseTimer` does the Stopwatch timing (60-tick rolling average). It is not state, not hashed,
not saved. Picking snaps the hit normal to the dominant axis and steps half a cell back into the solid cell
(`ViewCore.Picking.PickResolver`); a hit above `SliceY` or outside the world yields no pick (the ray is not
continued). `SliceController` owns `SliceY`; slice keys auto-repeat while held; each change queues terrain and water
of the chunk layers containing the old and new slice. Open jobs by kind shows "none" until the job board (M4-T6).
Consequences: Later tasks that want more overlay timings add a `TickPhase` value and bracket the call in `Tick()`.
The Godot layer (CameraRig, DebugOverlay, HoverMarker, GameRoot input) is build-verified only until a Godot .NET
binary is available.

## ADR-020: Screenshot presets computed from the world; placeholder plant meshes; harness structure (2026-09-26, M3-T6)
Context: docs/testing.md names four presets (`overview`, `river`, `hub`, `slice`) in one line each without camera
numbers. VIEW-20 does not say how the harness scene relates to Main.tscn, and the backlog asks for placeholder
"trunks/cones" for plants without a spec. The hub and river positions depend on terrain generation (GEN-05 may shift
the spawn flat). Godot .NET cannot be run on the build machine.
Decision: `ViewCore.Screenshots.ScreenshotPresets` computes each shot from the simulation: `overview` = world center,
yaw 45, pitch 45, distance 120 (max zoom), no slice; `hub` = hub footprint center at its origin height, yaw 45,
pitch 50, distance 32; `river` = halfway (in Z) between the hub center and the topmost water cell of the nearest wet
column on the hub's X line, yaw 90 (looking along -X so hub and river sit side by side), pitch 40, distance
max(40, 1.5 x span); `slice` = slice at y=20 over the tallest column (the hill peak), yaw 45, pitch 55, distance 50,
focus at y=21. Shots go through `OrbitRig.SetView`, which clamps like user input and sets `BaseFocusY` to the focus
height so the rig's per-frame update keeps the view. `ScreenshotArgs` parses the user args (defaults: seed 1, 1200
ticks, all four shots, `artifacts/screens`) and rejects unknown keys or shots. `scenes/Screenshot.tscn` is a `Node`
root with `ScreenshotRunner` that instances Main.tscn as its `Main` child, so the harness always renders the real
game scene; the runner pauses the real-time loop, runs the ticks at once, then per shot sets the slice, remeshes
everything unbudgeted, waits 3 drawn frames and saves the viewport PNG; exit code 0 ok, 1 save failed, 2 bad args.
Plants: `ViewCore.Meshing.PlantMesher` builds one world-space mesh: tree = 0.4-wide trunk box over its 4 trunk cells +
a square cone canopy (half-width 1.2, from base+2 to trunk top+1.5); bush = small cone (half-width 0.4, height 0.9) in
its cell; palette `plants.*` colors. Slicing hides plants whose base is above the slice, cuts trunks at the slice
layer top, and hides a canopy unless the whole trunk is visible. `scripts/screenshot.sh` checks `GODOT_BIN` is set,
executable and reports a 4.6 mono version (clear error otherwise), converts paths with `pwd -W` on Git Bash, uses
xvfb-run only on Linux without DISPLAY, and fails if any requested PNG is missing.
Consequences: Presets follow worldgen changes automatically. Changing preset numbers is a view-only change. When
chopping arrives (M4-T7) the plant mesh rebuilds on a plant-count change. The Godot side of the harness is
build-verified only until a Godot .NET binary is available.

## ADR-021: River springs every 16 cells hold the river's volume (2026-09-26, M3-T8)
Context: Gate G1 Q4. Seed 1 lost ~43% of its water over 10 days (4,785,289 -> 2,747,763). Cause: the bed is flat
(ADR-009), water moves only by level difference (WAT-05, WAT-08), the single GEN-08 inlet at x=0 is pinned at `Full`
and the drains at x=127 are pinned at 0. The steady state of that system is a linear slope from full to empty along
the whole river, i.e. about half the initial bank-filled volume, whatever the inflow strength. `SourceStrength`
cannot exceed 100 usefully (a source cell is capped at `Full`), and more source cells at x=0 only widen the inlet;
neither changes the slope. The human ruled out shrinking the fill or weakening the drains.
Decision: "More source cells" along the course. `TerrainShape.RiverSpringSpacing = 16`: every channel cell
(`ChannelDistance == 0`) in columns x = 0, 16, 32, ..., 112 (never the drain column) is a source for y = bed..bed+3,
the same y range as the GEN-08 inlet. Between springs the river stays at its fill level; only the last 15 cells
before the drains slope. Springs obey `SourceStrength` like the inlet, so drought (strength 0, M6-T4) still stops
all inflow. GEN-08 in `docs/specs/world.md` now points here; `TerrainGeneratorTests.River_CrossesMap_WithSourcesAndDrains`
asserts the spring layout instead of "all sources at x=0".
Consequences: Seed-1 volume after pre-settle is 4,914,791 at tick 0 and 4,905,846 at day 10 (-0.2%). Active water
cells drop from ~1,900-2,460 to ~670-720 (WAT-P2 budget 3,000). A pump or dig next to a spring column is refilled
at up to `Full` per cell per tick; that is the intended "river keeps flowing" behavior. Golden regenerated.

## ADR-022: Flat-color ambient light in Main.tscn; screenshots use a different renderer than play (2026-09-26, M3-T9)
Context: Main.tscn had only the sun (DirectionalLight3D), so faces turned away from it and cast shadows rendered
pure black. The human asked for "ambient up some", with the sun, water colors and palette unchanged. While tuning,
two renderer facts showed up: `screenshot.sh` renders with `--rendering-driver opengl3` (Compatibility), which
lights in gamma space, so any ambient brightens lit faces a lot there; the game plays with Forward+ (Vulkan),
which lights in linear space and also shows the vertex colors lighter/desaturated than the palette, because the
materials do not set `VertexColorIsSrgb`.
Decision: A `WorldEnvironment` with an `Environment` sub-resource in Main.tscn (so Screenshot.tscn inherits it):
`ambient_light_source = Color`, `ambient_light_color = (0.75, 0.78, 0.85)` (neutral, slightly cool, so shadows
stay the hue of the block), `ambient_light_energy = 0.3`; `reflected_light_source = Disabled` so the water gets no
background reflection it did not have before. Background stays the default clear color; no sky, tonemap, glow or
SSAO. The energy is tuned for Forward+, the renderer the human plays with: lit grass moves 143,171,119 -> 156,188,133
(about +9%), shadowed grass 0,0,0 -> 68,87,63. 0.5 washed the scene out in Forward+. The sRGB vertex-color issue is
not fixed here (it would change how the palette looks in play); it is flagged for the human.
Consequences: `SceneLightingTests` guards the environment and the unchanged sun. Compatibility screenshots
(`artifacts/screens/*.png`) look brighter than play (lit grass 85,125,59 -> 122,187,84); Forward+ renders are in
`artifacts/screens/forward_plus/` for comparison.

## ADR-023: PathGrid flag cache: world log cursor, water and plant hooks, sync on every query (2026-09-26, M4-T1)
Context: PTH-03 asks for a lazy `byte[]` flag cache invalidated by block and water changes. The existing
`PathGridTests` (and later systems) change blocks or water outside `Simulation.Tick` and query at once, so an
invalidation that runs only at tick end would serve stale flags. `VoxelWorld.ChangedCells` is cleared every tick
(and by WorldFactory/ScenarioBuilder), water levels change inside the CA step without any log, and plant
occupancy (WLD-07) also feeds PTH-01. Generators and ScenarioBuilder write blocks raw, without the log.
Decision: One flag byte per cell: `Valid | Standable | Walkable | Wet` (Wet = level > 0, for the PTH-07 wading
cost). Flags are computed on first query. Invalidation clears the cell and its 3x3x3 neighborhood (PTH-03) and
comes from three feeds: (1) blocks: `VoxelWorld.ChangeLogBase` (count of cleared log entries) plus a PathGrid
cursor; every query and `Simulation.Tick` (before `ClearChangeLog`) call `SyncWorldChanges()`, and if entries were
cleared unseen the whole cache is dropped; (2) water: `WaterGrid.WalkClassChanged(index)`, fired only when a
cell's level crosses dry/wet (0) or shallow/deep (`Full / 2`), at every level write site (SetLevel, sources,
deltas, drains, push-out); (3) plants: `PlantSystem.OccupancyChanged(index)` on trunk/bush add/remove.
`InvalidateAll()` is for raw writers (ScenarioBuilder.Build calls it; M4-T10 load must too). The cache and the
hooks are derived data: not hashed, not saved. `FlagComputations` is a diagnostic counter.
Consequences: Queries are correct mid-tick, which the job pipeline needs. The state hash is unchanged (seed-1
24,000-tick hash still `0f27c8ee613dd61c`). Anything that later affects walkability (M5-T2 construction sites)
must also invalidate through PathGrid. Regions (M4-T3) can hook the same invalidation points to mark itself dirty.

## ADR-024: A* start/goal rules, shared move rules, pooled search state (2026-09-26, M4-T2)
Context: PTH-04..12 do not say what happens when the start is not walkable, when a goal is not walkable, or when
the start is itself a goal. WAT-14 flee needs a path out of a deep (standable, not walkable) cell. PTH-13 regions
must use "the same neighbor rules" as A*.
Decision: The neighbor and cost rules live in `PathMoves.From` (8 horizontal directions in `Int3.Horizontal8`
order; per direction at most one of dy = 0, +1, -1 can be standable, so each direction yields at most one move) and
`PathMoves.Heuristic`; A* and M4-T3 regions both call them. PTH-06 intermediates are checked at a.y for flat and
down moves and at b.y for up moves; PTH-05 headroom is `a+up+up` for up and `b+up+up` for down. The wading cost is
added when the destination cell holds any water (walkable + wet = wadeable). Start: must be standable, else
`InvalidStart` (deep start allowed, for flee). Goals that are not walkable are dropped; none left gives `NoPath`;
a goal equal to the start returns `[start]` at cost 0, even when the start is deep (the agent is already there). The multi-goal heuristic is the minimum over goals
(consistent), so a closed node is never reopened. The heap holds `(f, h, index)` with stale entries skipped on pop.
`TooFar` is returned when a node would be expanded after 20,000 expansions. Search arrays (`g`, `cameFrom`, seen
and closed stamps, goal marks) are sized to the world on the first search and reused with a generation stamp.
Consequences: Search state is scratch, not sim state: not hashed, not saved. Warm informal timing on seed 1
(pairs 60-75 cells apart in x/z, paths about 120 cells): median 1.1 ms, p95 about 4 ms, up to about 10k
expansions when the path detours around the river. PTH-P1 (1.5 ms p95) is measured in M4-T12, which will likely
need per-expansion speedups (flat-index neighbor walk) and must define its sampling of "~100-cell" paths.

## ADR-025: Regions: full flood fill keyed on a PathGrid walkability version (2026-09-26, M4-T3)
Context: PTH-13 says regions are recomputed fully "when flagged dirty (any walkability change)" but not how the
change is detected. PathGrid's lazy cache cannot report old-vs-new walkability. On seed 1 the settled river makes
dry/wet and shallow/deep crossings every tick (about 17 per tick, ~0.5 deep crossings per tick), and a full rebuild
costs about 4-5 ms (Release), so rebuilding on every water crossing would cost ~4 ms per tick.
Decision: `PathGrid.WalkabilityVersion` is bumped on any block change (seen through the change-log sync), any plant
occupancy change, `InvalidateAll`, and a shallow/deep crossing in a *standable* cell (`WaterGrid.WalkClassChanged`
now passes a `deepChanged` flag). Dry/wet crossings (wading cost only) and deep crossings in non-standable cells
(which are never walkable) do not bump it. `Regions` rebuilds at ARCH-01 step 11 when the version differs from the
one it was built at, or after `MarkDirty()`; `Simulation` counts rebuilds in `Counters.RegionRebuilds`. The fill
scans flat indices ascending (cheap prefilter: not solid and solid below), seeds a BFS from each unlabelled walkable
cell with `PathMoves.From`, and numbers regions 1, 2, ... in that order, so ids are deterministic. Move rules are
symmetric between walkable cells (step up/down headroom and diagonal intermediates mirror), so a region is exactly
an A*-connected set; `RegionRuleTests.SameRegion_IffPathExists` checks this on random rough terrain. Region data is
derived: not hashed, not saved; M4-T10 load calling `PathGrid.InvalidateAll()` also dirties regions.
`PathGridCacheTests.Cache_ComputesEachCellOnceUntilInvalidated` used the global `FlagComputations` counter across a
`Tick()`, which now includes the first region build querying every candidate cell; the test now builds regions
first and then checks that neither a quiet tick nor a repeat query recomputes the cell, and that a neighboring
change recomputes it exactly once (same intent, the counter was a proxy).
Consequences: seed 1 rebuilds about 14 times per 500 ticks (one drain-side bank cell oscillates around half depth);
headless 24,000-tick median tick unchanged at ~0.03 ms. PTH-P2 (25 ms) has ~5x headroom; M4-T12 measures it.

## ADR-026: Path following, one repath per move, colonist spawn (2026-09-26, M4-T4)
Context: PTH-15/16 give step times and "re-check walkability before entering the next cell; repath once; if that
fails the step fails", but not when the check runs, what "once" counts, how a job step learns the outcome, or where
exactly the five colonists stand. The hub is placed without an event, and `RiverTests` requires a freshly created
seed-1 world to have no pending events.
Decision: `AgentMovement` (static, called by `AgentSystem`) follows `Agent.Path`. A segment starts when
`MoveTotal == 0`: the step must still be a legal `PathMoves.From` move (walkable, headroom, no corner cutting), then
`NextCell` and `MoveTotal` (PTH-15: 4 straight / 6 diagonal, +2 if `to` is higher, +3 if `to` is wet, matching the
PTH-07 wading rule) are set; progress counts one per tick, and the step is checked again just before `Cell` changes,
so an agent never enters a cell that was blocked mid-step. A blocked check repaths from `Cell` to the last path cell;
the tick is spent on the repath and the new path starts next tick. The repath targets only the goal the first path chose, even for a multi-goal `MoveTo` (PTH-16 "same goal"). Dead agents cannot start a move. One repath per `MoveTo` (`Agent.Repathed`): a
second block, or a repath that finds no path, sets `MoveStatus.Failed` and clears the path. `Agent.Move`
(`None/Moving/Arrived/Failed`) is the outcome M4-T6 job steps read; `Move` and `Repathed` are hashed (and must be
saved in M4-T10). `MoveTo` while mid-step abandons the step (the agent is still on `Cell`). Agents never block or
see each other (PTH-17). `SimCounters.PathSearches` now mirrors `Pathfinder.Searches` at tick end (for VIEW-17).
Colonists: after the river pre-settle, a breadth-first walk from the hub entrance with `PathMoves.From` (fixed
neighbor order) takes the first five dry walkable cells, one dwarf each, named from the Dvergatal in ASCII
(`Dvalinn, Althjofr, Nyradr, Reginn, Hanarr`, `WorldFactory.ColonistNames`). `AgentSpawned` is emitted by
`AgentSystem.Spawn`, but `WorldFactory.Create` now drains events once at the very end (it used to drain after the
pre-settle), so the initial world, like the hub, is read by the view rather than announced.
Consequences: seed-1 golden regenerated (five agents in the state). A mid-step `MoveTo` snaps the view back up to
one cell; acceptable for the POC.

## ADR-027: WorldActions rules, item piles, all-or-nothing storage (2026-09-26, M4-T5)
Context: ARCH-07 lists the actions and result codes but not the check order, what a building's "target cell" is,
which blocks `PlaceBlock` may place or what it costs, how partial deliveries behave, or where ECO-08 looks for a
free cell. No pile store existed.
Decision: New `ItemPiles` (`Simulation.Piles`): `SortedDictionary<cellIndex, ItemStack>`, one item type per cell,
emits `ItemPileChanged`, hashed in cell-index order (save in M4-T10). Every action checks, in order: actor exists
(`InvalidTarget`) and is alive (`AgentDead`), an id target (plant, building) exists, reach (target is the actor's
`Cell` or one of its 26 neighbors; for a building, any footprint cell), then the target cell's contents and other
preconditions; any non-Ok result changes nothing. An out-of-reach cell target is `OutOfReach` whatever it holds.
`Dig`: solid + diggable block (WLD-05 via `blocks.json`; y = 0 always refused) → Air, drop as a 1-item pile on the dug
cell; `Blocked` if the cell is the floor of a living agent (its `Cell` or `NextCell`; JOB-09 for the digger and every
other agent), of a plant, or of a building (`BuildingSolid` above; keeps the API safe for a future avatar,
DSG-02 only guards designations). `Chop`: marked trees only, reach to the base, 4 logs on the base. `PlaceBlock`: Air cell
→ a natural block (solid and diggable: Stone/Dirt/Grass/Sand/Farmland), `Blocked` by an agent in the cell or in the
cell below it (headroom), a plant, or a pile; no material is consumed (no task needs a cost yet; the future avatar
task adds one). `PickUp`/`PickUpFromStorage`: carried stack must be empty or the same item (`WrongItem`) and stay
≤ 10 (`InventoryFull`), then the source must hold the count (`NotEnoughItems`). `DeliverTo` and storage are
all-or-nothing: the whole carried stack goes in only if the building accepts the item (BLD-11) and has room under
both the per-item and total caps, else `WrongItem` / `StorageFull` and nothing moves (haul reservations in M4-T8
keep this from failing in normal play). Storage actions need a `Complete` building with storage; emptied entries
are removed from `Stored`. `Consume`: one unit, needs clamped at max, non-food/drink → `WrongItem`. `Drop`: the whole
stack on the target cell if it is standable (PTH-01, so never mid-air or in a trunk) and holds no other item, else the first cell in a fixed
spiral (radius 3 by horizontal Chebyshev ring, then x/z Manhattan, then same level / one up / one down, then z, then
x) that is standable and holds no other item (a same-item pile counts as free); none → `Blocked`. "Radius 3" is horizontal; the search also looks one level
up and down.
`Work(actor, WorkTarget)` checks the actor and reach only; step progress lives on the agent. Construction-site
delivery and construction progress (BLD-06..08) and pump cycles (BLD-13) extend `DeliverTo`/`Work` in M5-T2/M5-T4.
Consequences: seed-1 golden regenerated (the hash now includes the empty pile set). `Dig`/`Chop` put their drop on the
dug cell / tree base (always pile-free), so a dig in an overhang, or under an existing pile, can leave a pile without
a floor; acceptable for the POC. The spiral ignores regions, so a fallback pile can land where haulers cannot reach.

## ADR-028: Job board, step runner, reservations, failure handling (2026-09-26, M4-T6)
Context: JOB-03..08 define the board, priorities, selection order, reservations and the 5-failure rule, but not
the step representation, what "preconditions" are, when a step starts, what happens to a carried stack when a job
fails or the drop is blocked, or where the "Unreachable" mark lives before M4-T7 adds designations.
Decision: `JobBoard` (`Simulation.Jobs`) is storage only: `SortedDictionary<JobId, Job>`, monotonic ids, and four
reservation tables (cell → job, pile cell → reserved count, storage in/out by (building, item)) derived from the
claimed jobs (not hashed; a load calls `RebuildReservations`). A `Job` holds plain-data `JobStep`s (GoTo with goal
mode Exact / Reach / Building, Work(ticks), and one step per `WorldActions` call) and its declared `Reservation`s;
it is hashed in full. `JobRunner` (called from `AgentSystem.Tick`, ascending id) does one thing per agent per tick:
an idle agent (at most once per 5 ticks, `Agent.NextJobSearchTick`) first drops any leftover stack, then selects;
a claimed job's current step runs the same tick. Selection (JOB-06): open, unclaimed, non-need jobs past
`RetryAfterTick`, best by (priority desc, 3D Manhattan distance to `Job.Target` asc, id asc); the region filter
(some goal cell of the job's first GoTo in the agent's region, no A*) and the preconditions run only for a job that
would beat the current best. Preconditions are the job's reservations: `CanReserve` (cell free of other jobs, pile
has the unreserved count, storage has the unreserved stock / free capacity). GoTo goals are walkable cells within
ARCH-07 reach of the target (ascending index); the step starts a `MoveTo` on its first tick and advances movement on
later ticks; Arrived → next step, anything else → failure. Work calls `WorldActions.Work` each tick for `Ticks`
ticks. Any non-Ok action result, or a GoTo with no goals / a failed move, fails the job (JOB-08): failure counted
(`Counters.JobsFailed`), reservations released, agent halted and idle, carried stack dropped on its cell (ECO-08
spiral); at the fifth failure the job is removed and, for Dig, the designation cell becomes `DigUnreachable`; else
`RetryAfterTick = now + 50`. If the drop is `Blocked` the agent keeps the stack and retries the drop before each job
search (it takes no work until it succeeds). Completion releases reservations, removes the job, clears a Dig mark
(DSG-07), counts `JobsCompleted`. JOB-07 `JobRunner.AssignNeed` posts a need job and claims it at once, releasing a
current non-need job (no failure, no cooldown, stack dropped); it refuses (posts nothing) when the agent already runs
a need job or the reservations cannot be taken. `JobRunner.Cancel` (DSG-06) releases and removes. A minimal
`DesignationMap` (`Simulation.Designations`, DSG-01 byte per cell, hashed as non-None entries) exists now for the
JOB-08 mark; M4-T7 adds the commands and posting.
Consequences: seed-1 golden regenerated (board and designation state in the hash). Pile reservations are held until
the job ends, even after the pick-up, so a partly taken pile looks more reserved than it is for the rest of that
job; acceptable until M4-T8 refines hauling. A dead agent's claimed job is not released yet (M5-T5 death must call
`JobRunner.Cancel`-like release). Chop give-up has no mark yet (M4-T7). A job returned to the board restarts at step 0. The region filter
requires every GoTo leg to have a goal in the agent's region. A haul that fails after its pick-up drops the stack,
so its pile reservation may never fit again: M4-T8 must re-post or cancel such jobs.

## ADR-029: Designation commands, dig/chop posting, DSG-04 cap, DSG-08 deferral and step-aside (2026-09-26, M4-T7)
Context: DSG-02..08 and JOB-09 leave open: box corner order and out-of-world boxes, what "under a building" means,
whether re-designating a `DigUnreachable` cell retries, what a DSG-04 "job batch" is and whether the height bonus is
bounded, how "deferred" (DSG-08) behaves when an idle agent never leaves, where a chop give-up is recorded, and how
the two acceptance tests that end in hub storage can pass before hauling (M4-T8) exists.
Decision: Commands `DesignateDig(A, B)`, `DesignateChop(X0, Z0, X1, Z1)`, `CancelDesignation(A, B)` (tags equal the
type names) order their corners and intersect with the world; a box with nothing inside is rejected with
`CommandRejected` (still logged). DSG-02 skips y = 0, non-solid and non-diggable cells, building footprint cells,
the cells directly under them, and plant floors (`WorldActions.Dig` refuses those); `Dig` stays `Dig`; `DigUnreachable` becomes `Dig` (the player's retry). DSG-05 marks
trees only (bushes are harvested) and clears a chop give-up. DSG-06 clears marks in the box, unmarks trees whose base
is in the box, and `JobRunner.Cancel`s Dig jobs targeting the box and Chop jobs of those trees. `DesignationSystem.Tick`
(ARCH-01 step 8, stateless) posts `GoTo(cell, GoalMode.Dig) → Work(hardness) → Dig` with a cell reservation for each
`Dig` mark on a solid, diggable, exposed cell without a Dig job, and `GoTo(base) → Work(80) → Chop` for each marked
tree without a Chop job and not given up; marks in ascending index, trees in ascending id. A `Dig` mark on a
non-solid cell with no job, or a `DigUnreachable` mark on a non-solid cell, is cleared. DSG-04: the "batch" is every live `Dig` mark; an unclaimed dig job's priority
is 25 + min(y − lowest marked solid y, 4), recomputed each tick. The cap keeps dig (≤ 29) below Plant (30) so a tall
designation never starves farming, construction or delivery. `DesignationMap` keeps a sorted index of marked cells
so iteration costs the mark count. JOB-09: `GoalMode.Dig` goals are the reach cells except the one on top of the
target; if any of them has a floor not marked `Dig`, only those are used. DSG-08: a Dig job is not selected while
another living agent holds the target's top cell (`Cell` or `NextCell`); a claimed Dig step waits (no failure) while
one does, up to 200 ticks, then fails normally. So such a wait cannot last forever, an idle agent that found no job
and stands on a `Dig`-marked block walks to a cell in its region within a 5×3×5 box whose floor is not marked
(step-aside). Chop give-up (fifth failure) sets the new hashed `Plant.ChopUnreachable`; no job is posted for that tree
until it is designated again. Tests: `DigStone_EndsInHubStorage` and `Chop_MarkedTrees_LogsHauled` have full bodies
but stay skipped under `M4-T8` (they need `HaulSystem` and `ScenarioBuilder.Hub`); M4-T7 covers their dig/chop halves
with `DigStone_LeavesStonePile` and `Chop_MarkedTrees_DropLogs`.
Consequences: seed-1 golden regenerated (the plant hash now includes `ChopUnreachable`; with the field left out the
old golden still matched, so behavior on seed 1 is unchanged). The tick scans the job board, the marks and the plant
list every tick (O(jobs + marks + plants)); seed 1 with ~670 marks and 150 trees: tick median 0.056 ms. A pit deeper
than 5 levels relies on exposure, not priority, below the capped band. Step-aside ignores other agents' targets, and
a 3-level pit can leave an agent at its bottom with no way out (no ramps); acceptable for the POC.

## ADR-030: Pile hauling, storage choice, re-planned haul jobs, reservation-aware storage actions (2026-09-26, M4-T8)
Context: JOB-10 says "one Haul job per pile when some storage accepts the item with free capacity", "nearest by
Manhattan", "capacity is reserved on claim", but not: nearest to what point of a building, how many items one job
moves, what happens when the chosen storage fills between posting and claiming, how a haul that fails after its
pick-up is cleaned up (ADR-028 left this open), whether the BLD-10 reserved counts bind `WorldActions`, and how a
reservation for one item counts against a total cap (warehouse).
Decision: `HaulSystem` (ARCH-01 step 9, stateless) keeps at most one pile haul per pile cell:
`GoTo(pile, Reach) → PickUp(pile, item, n) → GoToBuilding(storage) → DeliverTo(storage)` reserving `FromPile(n)` and
`IntoStorage(storage, n)`, target = the pile cell. Storage choice: complete buildings with storage that accept the item
(BLD-11) and have unreserved room, min Manhattan from the pile cell to the building's entrance cell, ties by lower id.
`n = min(pile count − other jobs' pile reservations, 10 (carry cap), room)`, so a big pile or a nearly full storage
takes several jobs in turn. Every tick, each unclaimed pile haul is re-planned from the current pile and room (steps
and reservations rewritten; failures and cooldown kept) or withdrawn (`JobRunner.Cancel`) when its pile is gone or
nothing has room; claimed hauls are left alone. So a haul that fails after its pick-up (stack dropped elsewhere) is
withdrawn and the new pile gets its own job. Pile hauls are recognised by kind + step shape, so other Haul-kind jobs
(tests, later pump hauls) are not touched. Room (`JobBoard.StorageRoom`) = min(per-item cap − stored − reserved in for
that item, total cap − total stored − reserved in for all items), 0 = no cap; a new `ReservedInTotal` table backs the
total. `WorldActions.DeliverTo` needs that room and `PickUpFromStorage` / `Consume` need stock not reserved out, both
counting the actor's own claimed job's reservations as its own (so an avatar or an unreserved job cannot take room or
stock another job was promised). BLD-12: `BuildingSystem.Totals` sums `Stored` over all buildings at the buildings
step; derived, not hashed or saved.
Consequences: seed-1 golden unchanged (no piles without commands). A job cancelled after five failures is re-posted
by the next tick with a fresh failure count; the region filter and the reservations make repeated haul failures
unlikely, so no give-up mark is kept. The storage choice ignores regions: if the nearest storage is unreachable from
the pile while a farther one is reachable, the pile waits. A partly hauled pile gets no second job until the first
ends (one job per pile). `Totals` is computed before the haul and agent steps, so it lags delivery by one tick.

## ADR-031: Flee search swims through deep water; trapped agents; need-job failure (2026-09-26, M4-T9)
Context: WAT-14 says an agent standing in a cell that becomes deep "requests a path to the nearest non-deep standable
cell" and is trapped if "none reachable within 64 steps". A* never enters deep cells (PTH-02), so from the middle of a
flooded area it can only ever take one step; "within 64 steps" implies the flee path may cross deep water. The spec
does not say how "nearest" is measured, what a trapped agent does with its job, how often it searches again, or what
happens to a per-agent need job that fails (JOB-08 returns failed jobs to the board, where need jobs are never
selected, so they would leak).
Decision: `Pathfinder.FindFlee(start, 64)` is a breadth-first search over the PTH-04..08 moves in swim mode
(`PathMoves.From(..., swim: true)`: deep standable cells count as passable for the destination and the PTH-06
intermediates; every other rule is unchanged). It returns the fewest-step path to the first walkable cell found in
the fixed `Horizontal8` expansion order (Cost = step count); a walkable start is `[start]`. `FleeRules.Tick` runs for
each living agent just before its job step (ARCH-01 step 10): when the agent's cell is standable and deep and it has
no Flee job, it searches; on success it claims a Flee need job (`GoTo(dry, Exact)`, priority 200, preempting any
job including Drink/Eat, JOB-07) and follows the found path at once; the GoTo step of a Flee job moves in swim mode
and a blocked step fails the job without an A* repath. The first search is immediate, whatever the JOB-06 idle
search throttle says. On no path the agent is trapped (new `AgentState.Trapped`, hashed with the state): its job is
released (stack dropped), it stops, takes 1 health damage per tick, does nothing else, and searches again every 5
ticks (`NextJobSearchTick`); it returns to Idle when its cell is no longer deep. At health 0 `AgentSystem.Kill(Drowned)` releases the job,
drops the stack (if that drop is blocked, the stack is lost with the dead agent), marks the agent dead and emits `AgentDied`. Need jobs (Drink, Eat, Flee) that fail or are preempted
are removed from the board (their owner re-posts them), still counted in `JobsFailed` when they fail.
Consequences: No new sim state (the Flee job is a hashed board job; health is hashed). Seed-1 golden unchanged.
Walkable means "not deep and not a construction site" (PTH-02); swim mode uses standable, so a fleeing agent could
also cross an M5 construction footprint. The BFS runs only for agents in deep water (at most once per 5 ticks while
trapped); in a wide lake it can visit ~(2·64+1)² cells. Health regeneration (ECO-06) comes with M5-T5.

## ADR-032: Save format v1: section markers, what is saved, derived data rebuilt on load (2026-09-26, M4-T10)
Context: SAV-01 lists the sections but not the encoding of each, whether queued-but-unapplied commands are saved,
how an unknown command is handled, or what happens to derived data (path flag cache, regions, reservation tables,
storage totals, the WAT-15 WaterDirty throttle). Farm tiles, moisture and weather do not exist yet.
Decision: `SaveGame` writes magic `CSAV`, `int` version 1, then each section preceded by a 4-byte marker
(`0x53454300 + n`, `SaveSection` enum; a mismatch fails with the expected section's name). Header: world size, seed,
tick, RNG state, and whether regions were built and current. Blocks and water levels: run-length pairs (7-bit-encoded run length, value). Water: active set
(sorted, it is hashed state, ADR-010), source strength, sources and drains in list order, then the four stats.
Then plants, buildings (by def id string) + delivered, storage contents, item piles (cell index order),
designation marks (cell index order; `DesignationMap.Set` rebuilds the sorted index), agents (alive only, SAV-06;
every field incl. name, path, move state, `NextJobSearchTick`), jobs (every field incl. steps and reservations), the
four id allocators, the command log (tick + `CommandCodec`), and the pending command queue (so a save between an
`Enqueue` and the next tick loses nothing). `CommandCodec` lists every command type; saving a command with no codec
throws `NotSupportedException` naming its tag, and every task that adds a command must add it there. Loading throws
`InvalidDataException` for a wrong magic, a version mismatch (message names both versions), a truncated file, an
out-of-range count/index/enum, or a building def missing from the ContentDb. Load writes blocks and levels raw,
restores entities through `internal Restore` hooks (no events), then: clears the change log, marks all chunks dirty,
`PathGrid.InvalidateAll()`, rebuilds regions at once when the saved game had them built (job selection reads them
during the tick; a game saved before its first tick had none and its load has none either), `Jobs.RebuildReservations()`, takes the loaded levels as the WAT-15
baseline, and drains events. Farm tiles and weather are not sections yet; the tasks that add them (M6-T1..T4) add
sections and bump the format version (no migration, SAV-04). Moisture is recomputed, not saved (SAV-01; superseded by ADR-046: moisture is saved).
Consequences: a save taken between ticks is exact, also before the first tick. Only a game whose world was mutated
outside `Tick` after its regions were built (stale regions, test setup only) loads with fresh regions instead.
Block bytes, item ids (1-based), enums, counts and indices are range-checked on load. `SimCounters` and `BuildingSystem.Totals` are
diagnostics/derived and restart after a load (Totals fills at the next buildings step). Seed-1 save at day 5 is
29,376 bytes (SAV-05 budget 3 MB; save ~20 ms, load ~52 ms in a Debug test run).

## ADR-033: Godot tools, entity views, quick save and screenshot scripts (2026-09-26, M4-T11)
Context: VIEW-08..13, 16 and 19 leave several details open. They do not say how a 2D drag becomes a 3D box for each
tool, whether agents and piles obey the slice, how the view notices designation changes (there is no event), how
trapped agents look, what the Farm/Build/Deconstruct buttons do before their systems exist, or how the screenshot
harness can show colonists at work.
Decision: The drag logic is `ToolController` (ViewCore, tested). Dig uses the first corner cell and the second corner
cell, with the second corner's Y clamped to `SliceY` (`DesignateDig`). Chop is the X/Z rectangle of the two corners
(`DesignateChop`). Cancel sends the sorted box raised one cell, so dragging over ground also cancels the trees
standing on it. A right click or a tool change drops the drag. Agents and piles follow the plant slice rule: they are
hidden when their cell Y > `SliceY`. Agent positions are lerped along the current move step, including the sub-tick
accumulator fraction. Agent colors come from `palette.json` `agents`, which gains a `trapped` color. Dead agents lie
on their side. Designation marks emit no events, so `DesignationRenderer` rebuilds when an FNV signature over the marks
and marked trees changes. Unreachable dig cells and trees are drawn red. Piles rebuild on `ItemPileChanged` or a slice
change. A pile is 1 cube per 5 items, at most 8 cubes. Farm, Build and Deconstruct are shown as disabled buttons.
F5 writes `user://quick.save` through a `.tmp` file and then a rename, so a failed save keeps the old file. F9 swaps in
the loaded `Simulation` and rebuilds every sim-bound renderer, keeping the camera, slice and speed. Errors show as a
toast. The screenshot harness gains `--script <none|digchop>` (the `SCRIPT` env in screenshot.sh). `digchop` digs a
14x9x2 pit 3 cells east of the hub and chops trees within 24 cells of it; the commands are computed from the world
(ADR-020).
Consequences: A drag can only target cells at or below the slice. The designation signature costs one pass over the
marks per frame; this is fine at POC sizes and can become an event later if needed. `digchop` uses a 2-deep pit
because a deeper one strands every dwarf in it (see PROGRESS M4-T11).

## ADR-034: PTH-P1/P2 measurement and the coordinate-based search loop (2026-09-26, M4-T12)
Context: PTH-P1 says "p95 A* for 100-cell paths on seed 1" without saying how pairs are chosen or what "100-cell"
means; PTH-P2 does not say whether the flag cache is warm. The first measurement (defined below) gave p95 1.68 ms,
over the 1.5 ms budget. Each A* expansion went through `PathMoves.From` with `Int3` arithmetic, and every flag
read re-synced the world change log, checked bounds on an `Int3`, and recomputed the flat index.
Decision: `PathPerfTests` measures seed 1 after one tick. Pairs are drawn from the largest region's walkable cells
(ascending flat index) with `Rng(1234)`. Each pair's goal is 60..100 cells from the start on the larger horizontal
axis, and a pair is kept when A* finds a path of 90..110 cells (start and end included). The first 200 kept pairs
are timed once each after the selection pass, which also warms the cache. The p95 of the 200 times must be ≤ 1.5 ms ×
PERF_SCALE. PTH-P2 asserts the median of 30 full rebuilds ≤ 25 ms for both a warm cache (`MarkDirty`) and a cold one
(`PathGrid.InvalidateAll`, as after a load). Speedup: `PathMoves.Steps` (internal) holds the PTH-04..08 rules on raw
coordinates, with the same moves in the same order, and reads the new `PathGrid.FlagsAt(x, y, z)`. That method
checks bounds and computes the index inline and does not sync. A* and `Regions` sync the change log once per search
or rebuild, which is equivalent because a search never changes blocks. The public `PathMoves.From` now syncs and
wraps `Steps`, so every caller still shares one rule implementation. Flee BFS keeps using `From`.
Consequences: Release, this machine: A* median ~0.31 ms, p95 ~0.75-0.81 ms (max expanded 5,153); region rebuild
warm ~2.1 ms, cold ~3.5 ms. Behavior is unchanged: the golden hash is the same and headless seed 1 still gives
`d8a1e43aeeb5d540`.

## ADR-035: Screenshots render with Forward+ on the default driver (2026-09-26, M4-T13)
Context: ADR-022 left `screenshot.sh` on `--rendering-driver opengl3` (Compatibility), which lights in gamma space,
so gate shots looked brighter and more saturated than play. At G2 (early answers) the human asked for gate shots
in the renderer the game plays in, and to keep the current vertex-color look (no `VertexColorIsSrgb`).
Decision: `screenshot.sh` passes `--rendering-method forward_plus` and no `--rendering-driver`, so Godot uses its
default driver (Vulkan on Windows and Linux). The flag matches `project.godot` (features "Forward Plus", no
rendering-method override) and is explicit so a future project setting cannot silently change the gate renderer.
The separate `artifacts/screens/forward_plus/` comparison path is dropped. `SEED`, `TICKS`, `SHOTS`, `OUT` and
`SCRIPT` are unchanged. `ScreenshotRendererTests` guards the script, the project setting and docs/testing.md.
Consequences: gate shots now match play (lighter, greyer greens; shadowed faces lit by the 0.3 ambient). On Linux
under `xvfb-run` the shots need a Vulkan driver (e.g. Mesa lavapipe); the CI job does not run screenshots.
Supersedes the "Compatibility screenshots" consequence of ADR-022.

## ADR-036: The headless runner reuses the screenshot command scripts (2026-09-26, M4-GATE)
Context: G2 asks for a headless seed-1 run with a dig + chop script and job/haul/path stats. The only dig + chop
script is `ScreenshotScripts.digchop` in ViewCore; the headless runner referenced only Aurvangar.Sim, and the
survival script (test project) does not exist before M5-T7.
Decision: `tools/Aurvangar.Headless` also references Aurvangar.ViewCore (Godot-free, depends only on the sim) and
accepts `--script none|digchop`, enqueuing `ScreenshotScripts.For(name, sim)` before tick 1, exactly as the
screenshot harness does. So the headless stats and the gate screenshots describe the same work. With a script it
prints per-report work stats and a final summary; "items hauled" is the rise in building storage since tick 1.
Unknown names (including `survival`, until M5-T7) exit 2. Without `--script` the output and hash are unchanged.
Consequences: a tool depends on view logic, but only on pure C# code; the sim still never references ViewCore.
When M5-T7 adds `survival`, it must either move to a project the runner can reference or be added beside these.

## ADR-037: Digs never strand the digger: what-if connectivity to the Great Hall (2026-09-26, M4-T14)
Context: G2 answer 1b. A dwarf must not take or finish a dig after which its standing cell no longer connects to
the Great Hall's region; such a dig waits, and turns DigUnreachable once no other open dig can make it safe. Regions
(PTH-13) are only rebuilt at the end of the tick, so a trial region rebuild per candidate (~2 ms) is too slow for job
selection.
Decision:
- Digging block `T` only adds cells and moves (T and the cell below it may become standable; PTH-05 headroom grows),
  except for one loss: the cell on top of `T` (`U`) loses its floor. So the test is local first: if `U` is not
  walkable the dig cuts nothing; if `U` has at most one move neighbor, or all of `U`'s move neighbors meet again in a
  flood on the what-if view within 512 cells, it cuts nothing (`DigTrial.MaySplit`, cached per block until
  `PathGrid.WalkabilityVersion` or `PathGrid.DeepVersion` moves; the latter counts every shallow/deep crossing,
  because the what-if view reads the depth of cells that are not standable now, so a loaded game and a continuous
  run give the same answers). Otherwise an exact alternating flood from the stand cell and from the hall's
  reach cells on the what-if view decides; it stops when the sides meet or either runs out, so a stranded pocket is
  found in about twice its size. The what-if view is `DigTrialCells`, an `IMoveCells` struct; `PathMoves.Steps` is
  now generic over that view, so the what-if flood uses the same move rules as A* and regions (no copy).
- The rule is about the digger's stand cell (the human answer), with the Great Hall = the lowest-id complete
  building with def id `hub`. With no hall the rule is off (small test worlds without a hub behave as before). If
  the regions are up to date and the stand is already apart from the hall, the dig is allowed (nothing to cut).
  Mid-tick, after a change, regions are stale, so a two-sided flood on the live world answers "apart already?".
- Where: dig goal cells (JOB-09) drop stranding stand cells, so the PTH-13 region filter in job selection never
  takes a dig with no safe stand cell, and the GoTo never heads for one. At Work start and at the Dig step the agent
  re-checks its own cell; if the dig would now strand it, the job goes back to the board with the JOB-08 cooldown but
  no failure (it is not broken, it has to wait).
- "No other open dig can make it safe": each tick with dig jobs on the board, if none is claimed and none has a
  safe stand cell in a living agent's region (a dig no one can reach never runs), every dig whose stand cells all strand is removed and its mark turns DigUnreachable. A dig with
  no stand cells at all is left as before. Re-designating turns the mark back into Dig (DSG-02), which re-tries.
- The dug cell counts as dry in the what-if view (water has not flowed in yet).
Consequences: a pit dug top-down keeps its last step out per level; in the 10x7x5 seed-1 pit 347 of 350 cells were
dug and 2 turned red. Other dwarves are not protected by this rule (only the digger; extended to every dwarf by ADR-059); in pits they stay connected
through the digger's side in practice. The cost is a small flood per candidate dig whose top cell is walkable,
cached until walkability changes; perf budgets and the golden hash are unchanged.

## ADR-038: Dig drags mark tree floors; digs below a marked tree wait in steps (2026-09-26, M4-T15)
Context: G2 answer 2. DSG-02 skipped the cell under a plant because `WorldActions.Dig` refuses a plant's floor, so a
dig + chop drag over woods left one-cell pillars. The human asked that the drag also mark those cells, and that the
dig job wait until the plant is gone. A first cut (mark + wait only) showed a second problem: digs around the tree
went on while it stood, so in a pit it ended up on a pillar two or more levels above any standable cell, out of
reach (ARCH-07 reach is one level up or down), and the chop never ran.
Decision:
- DSG-02 marks plant floors like any other diggable cell (buildings are still skipped).
- DSG-03: no dig job is posted for a cell with a plant on top (tree or bush). When the plant is felled or removed,
  the next designation tick posts it as usual; the M4-T14 strand rule (ADR-037), DSG-08 and JOB-09 apply unchanged.
- While a tree marked for chopping (not `ChopUnreachable`) stands on a `Dig`-marked floor F, a dig mark at horizontal
  Chebyshev distance r >= 1 from F with y <= F.y - r gets no job, and an unclaimed job already posted there is
  withdrawn (the chop may be marked after the dig). What can be dug meanwhile forms 1-high steps down from the tree,
  so a cell beside its base stays standable and reachable. The hold ends when the tree is felled, unmarked or given
  up (`Designations/TreeFloors`). It is derived each tick from marks and trees, so no new state, save or hash field.
- A tree that is not marked for chopping keeps its floor mark waiting forever and holds nothing; the player chose to
  keep it.
- Logs dropped at a felled tree's base stay where they are when the floor below is dug (loose piles do not fall;
  this is the same as a stone drop under a later dig). They are hauled from a neighbouring cell as before.
Consequences: a drag over woods is dug completely once the trees are down. In deep pits the cells under and around
a marked tree are dug last. A chop is not prioritised over digs; the holds make the order safe instead. The
DSG-09 give-up can still turn a held-back region's digs red if every posted dig strands while a chop is pending;
re-designating retries them.

## ADR-039: Item piles draw as a fixed-size marker with a count label and a post over dug floors (2026-09-26, M4-T16)
Context: G2 answer 3. Piles were drawn as 0.3-cell cubes, one per 5 items, which read as brown specks at the hub
zoom. The human asked for a fixed-size marker readable at the default hub zoom plus a count label. Piles also do not
fall when their floor is dug (ADR-038), so a pile can hang over air.
Decision:
- VIEW-10 updated. Every pile is one marker whatever its count: a 0.74 x 0.45 box in the item's palette color with a
  lighter 0.52 x 0.1 cap (40% toward white), on the floor of the pile's cell (`PileMesher`, ViewCore).
- The count label is the bare number ("12") on a Godot `Label3D` billboard in fixed-size mode (about 20 px), anchored
  0.9 above the cell floor. It shows while the camera distance is <= 80 (default 60, hub preset 40); the 120-distance
  overview hides it to avoid clutter. The hover label ("Stone ×12") stays.
- A pile over non-solid cells gets a thin post (0.16 wide, darkened item color) down to the first solid block,
  capped at 8 cells, so it reads as "at this height" rather than being drawn somewhere it is not. `AtPick` walks up
  the air column above a picked floor (same cap) so hovering the floor under a floating pile finds it.
- The old tests `PileCubes_OnePerFiveItems_Capped` and `Piles_CubeStackInItemColor_HiddenAboveSlice` encoded the
  cube-per-5-items look the human replaced; they are replaced by `View/PileViewTests` (fixed size for 1 and 500
  items, colors, label text and anchor, slice, post depth and cap, hover through the post, label zoom rule).
Consequences: the pile count is readable at a glance; item type is by color (plus hover). No sim change.

## ADR-040: Placement validation details: check order, stackable flag, entrance of stacked levees, pump edge (2026-09-26, M5-T1)
Context: BLD-01..04 leave several points open. BLD-04 names the levee specifically, which would hard-code a
building id in C#. A levee placed on another levee on open ground has its entrance cell in mid-air, so the literal
BLD-02 entrance rule would forbid all stacking except next to raised ground. BLD-03 says only the cell in front of
the intake must be non-solid, which a pump on flat dry ground also satisfies, yet "pump away from water" must be
rejected. Blueprints do not exist yet (BLD-05 is M5-T2), but overlap between blueprints must be tested now.
Decision:
- `BuildingSystem.CanPlace` returns the first failure in this order: `BadRotation` (only 0/90/180/270), `PrebuiltOnly`,
  `OutOfBounds` (any footprint cell or the entrance), `Overlaps` (a footprint cell is in any building's footprint,
  whatever its state, or is another building's entrance), `FootprintBlocked` (a footprint cell is not Air or holds a
  plant; water is allowed), `NotOnGround`, `EntranceBlocked`, `NeedsWaterEdge`.
- New data field `stackable` (buildings.json, default false; true for the levee only). A stackable building may sit on
  a building of the same type in state Blueprint or Complete (not while under construction or being deconstructed). Ground under the bottom layer is a solid block that is not
  part of a building, or part of a complete stackable building; a warehouse roof is not ground. A blueprint is air,
  so only the same stackable type can stand on it.
- Entrance (BLD-02): standable (PTH-01) and not inside any building's footprint. A stacked building may instead use
  the standable cell one level below its entrance; the upper footprint cell is in reach from there (ARCH-07, 26
  neighbors). So levees stack two high from open ground, and higher only next to raised ground or other levees.
  M5-T2 must use that lower cell as the builder's stand cell when the entrance cell is not standable.
- Pump edge (BLD-03): the front cell is on the side opposite the entrance, level with the bottom layer, found at
  rotation 0 by moving the entrance across the footprint along the axis it lies outside (`BuildingShape.IntakeFront`),
  then rotated. Both the front cell and the intake cell (one down) must be in bounds and non-solid. Water is not
  required at placement; a pump on a dry bank is allowed and flags `NoWater` when it runs (BLD-13).
- ContentDb now rejects an unknown `placement` value and an entrance that lies inside the footprint or off y = 0.
- `BuildingSystem.TryPlaceBlueprint` creates the `Blueprint` building (state part of BLD-05, no blocks written) so
  overlap can be tested; the `PlaceBuilding` command, jobs and the `BuildingPlaced` event come in M5-T2.
Consequences: every placement reason is data-driven except the two placement kinds. Overlap and lookups scan all
buildings (a few dozen in the POC); an index can be added if building counts grow.

## ADR-041: Construction flow details: commands, site blocking, deliver jobs, cancel and deconstruct (2026-09-26, M5-T2)
Context: BLD-05..09 leave open how deliveries are split and sourced, what a cancelled or deconstructed building does
with its jobs and materials, how a site blocks agents, and what happens to dig marks under a blueprint.
Decision:
- Commands. `PlaceBuilding(defId, origin, rotation)` emits `BuildingPlaced`, or `CommandRejected` with the
  `PlacementResult` name or `UnknownBuilding`. `Deconstruct(buildingId)` cancels a blueprint or site and starts
  deconstruction of a complete building. Its rejections, checked in this order: `UnknownBuilding`, `PrebuiltOnly`,
  `AlreadyDeconstructing`, `BuildingOnTop` (another building covers a cell right above the footprint; stacked
  levees come down top first). `Deconstruct` is a non-positional record, because a positional one would generate a
  `Deconstruct` method, which C# forbids on a type of that name.
- Site cells (PTH-02) are neither walkable nor standable. `PathGrid.SetSite` marks them and bumps
  `WalkabilityVersion`. The marks are derived from building state and rebuilt in `SaveGame.AfterLoad`, so they are
  not saved or hashed. Because the cells are not standable, swimming (fleeing) agents cannot path through a site
  and ECO-08 never drops a pile in one.
- BLD-06 Deliver jobs.
  - Shape: `GoTo(source) -> PickUpFromStorage(source, item, n) -> GoTo(site) -> DeliverTo(site)`. The job reserves
    the source's stock (BLD-10). Its target is the site's entrance cell, and its site is the last step's target.
    Construction jobs are recognised by this shape (and Construct/Deconstruct by a last `Work` step on a
    building), so other code may still post plain Construct jobs on cells.
  - Count, per site and item, every tick: `open = remaining - sum of claimed delivers`. Keep `ceil(open/10)`
    unclaimed jobs of 10, 10, ..., rest. Unclaimed jobs are re-planned in place, extras are cancelled and missing
    ones posted.
  - Source: complete storage that accepts the item. First choice is the nearest (Manhattan between entrances, ties
    by lower id) with `n` unpromised in stock. Failing that, the one with the most stock, carrying only that much.
    Failing that, the nearest, carrying `n` (the job then waits for stock through JOB-06 `CanReserve`). With no
    such storage, no job is posted. Loose piles are not a source: JOB-10 hauls them into storage first.
  - BLD-04: while any building under the bottom layer is not complete, no Deliver jobs exist, and `DeliverTo` on the
    site is `Blocked`.
- `DeliverTo` on a site delivers `min(carried, remaining)`; any surplus stays carried and is dropped when the job
  ends. The first delivery starts construction (BLD-07):
  - Agents in the footprint are moved to the stand cell and halted. A walk in progress re-plans from there instead
    of failing.
  - Piles in the footprint move out via the ECO-08 spiral.
  - The stand cell is the entrance, or for a stacked building whose entrance is not standable, the cell below it
    (ADR-040).
  - The delivery that completes the materials posts the Construct job at once. The tick also keeps exactly one.
- Work steps of Construct and Deconstruct jobs end when the building is complete or gone, not after a fixed tick
  count. Progress lives on the building, so a new worker continues it. Building-mode goals exclude the building's
  own footprint and, while it is being deconstructed, the cells on top of it.
- Deconstruction:
  - Takes `max(1, buildTicks/2)` work ticks.
  - The last tick waits (DSG-08 limit) while an agent is on top of the building. It stands down (ADR-037) when
    removing any footprint cell would cut the worker off from the Great Hall. Each cell is tested like a dig; this
    is an approximation for multi-cell buildings.
  - On removal, the footprint becomes Air, and `floor(cost/2)` of each item plus everything stored drops as piles
    at the stand cell.
  - Cancel refunds all delivered items the same way. Items no agent carries are placed by the internal
    `WorldActions.PlacePile` and `MovePile`, so every item move still goes through `WorldActions`.
- Dig marks: placing a blueprint clears dig marks on the ground under its bottom layer and cancels their dig jobs.
  `WorldActions.Dig` is `Blocked` under any building footprint in any state. This extends DSG-02, which never marks
  a building's floor.
- `BuildingAt` uses a derived cell index (cell -> lowest building id) kept by add, remove and load. It is lookup-only.
- Only living agents are moved out of a new site; a dead agent's body stays where it is (it holds no cell). A
  building-work step keeps no tick count on the agent (the count is on the building); `StepProgress` only counts
  the blocked wait. The last-resort refund placement never uses a cell inside any building footprint.
Consequences: site jobs and blocking are derived and stateless beyond the building fields that are already saved,
so there is no save format change and the golden hashes are unchanged. BLD-04 ordering is now enforced for delivery.

## ADR-042: Pump details: cycle on the building, NoWater from the intake each tick, worker runs until relieved, buffer hauls (2026-09-26, M5-T4)
Context: BLD-13/14 leave open where the cycle counts, when NoWater is set and cleared, how long one OperatePump job
lasts, and how the buffer is hauled.
Decision:
- The buffer is the pump's `Building.Stored` (output item only). A complete producer's `Progress` counts the ticks of
  the current cycle; `Complete` resets it to 0 for producers (other buildings keep `Progress == buildTicks`). So a
  new worker carries on a cycle, and no new state, save field or hash field is needed.
- The cycle is `WorldActions.Work` on the complete pump. At `cycleTicks` the intake is read: at least
  `minIntakeLevel` removes `unitsPerCycle` through the new `WaterGrid.Pump` (counted in `WaterStats.Pumped`; WAT-11
  now reads `... - Evaporated - Pumped`) and adds 1 output; below it nothing is made and `NoWater` is set. `Work` on
  a full buffer is `StorageFull` and changes nothing.
- `NoWater` is also refreshed from the intake level every tick at ARCH-01 step 7 (`Pumps.Tick`), so a dry pump is
  flagged without a worker and clears on its own when water returns.
- One OperatePump job (`GoTo(entrance, Exact) -> Work(pump)`) exists while the buffer is below `buffer` and the pump
  is not NoWater. Its Work step runs cycle after cycle (jobs-agents.md "repeat until relieved") and the job is done
  when the buffer is full, the pump is NoWater, or the pump stops being complete; the check runs before every tick,
  so a worker never works a pump being deconstructed. An unclaimed job is withdrawn when not wanted; the job is
  cancelled when the pump is gone or deconstructing. Need jobs (JOB-07) still preempt it.
- BLD-14: one buffer Haul job per pump (`GoTo(pump) -> PickUpFromStorage(pump) -> GoTo(storage) -> DeliverTo`) while
  the buffer stock not promised to a claimed haul is at least `haulAt`. It carries all of that stock (at most 10 and
  the destination's room) to the nearest complete storage that accepts the item with room (Manhattan from the pump
  entrance, ties by lower id). Unclaimed hauls are re-planned every tick. `PickUpFromStorage` accepts a complete
  producer's output (and nothing else from it). The haul is recognised by its shape alone (a Haul whose second step
  is `PickUpFromStorage`), so it is never mistaken for a pile haul or a construction Deliver and is still found after
  its pump is gone. When the pump is gone or deconstructing, unclaimed hauls and claimed ones that have not picked up
  yet are cancelled; a haul already carrying the water delivers it. `NoWater` is false while a pump is not complete.
- Deconstructing a pump drops its buffer as a water pile at the stand cell (BLD-09 "anything stored"); pile hauling
  takes it to the hub.
Consequences: no save format change, golden hashes unchanged. A worked pump holds one dwarf until its buffer fills
(10 cycles, 300 ticks); with priority 40 it goes before digs, chops and hauls. ADR-040's "pump at any bank edge"
still applies; a pump placed on a dry edge just shows NoWater (flag for G3).

## ADR-043: Needs details: retry ticks, consume loop, regen while drowning, ColonyLost flag, starting stock, dig deadlock (2026-09-26, M5-T5)
Context: ECO-02..07 and JOB-07 leave open how often a failed need is retried, how many units a Drink/Eat job
reserves, whether a drowning agent heals, where the ColonyLost "once" lives, and which food an Eat job picks.
Decision:
- `NeedsSystem` (ARCH-01 step 6) runs per living agent in ascending id: decay (hunger -1, thirst -2, floor 0), then
  health -1 while either need is 0 (death at 0, thirst wins ties), else +1 on every tick with `tick % 10 == 0`,
  except while `Trapped` in deep water (WAT-14 drowning stays exactly 1 damage per tick).
- Below 4000 the agent posts Drink first, else Eat, through `JobRunner.AssignNeed` (claimed at once, preempts any
  non-need job; Flee still preempts Drink/Eat). Nothing is posted while it runs a need job or is trapped.
- Retry: new hashed + saved agent fields `NextDrinkTick` / `NextEatTick`. A try that finds no complete storage with
  unpromised stock in the agent's region, or a need job that fails (JOB-08), waits 100 ticks before that need is tried
  again (ECO-04). A completed or preempted need job allows an immediate re-post.
- Storage choice: nearest complete storage by Manhattan to its entrance, ties by lower id, that has unpromised stock of
  a matching item and a reach cell in the agent's region (PTH-13). Item: the lowest item id with stock (berries before
  potatoes). The job reserves `ceil((9000 - need) / value)` units (at least 1, at most the stock); each consumed unit
  shrinks the reservation (`JobBoard.UseStorageOut`). The Consume step eats/drinks one unit per tick until the need is
  >= 9000; if storage runs out after at least one unit the job ends normally; a first unit that cannot be had fails it.
  Drinking uses storage only (the hub); pump buffers reach it by BLD-14 hauls.
- HUD "No water" / "No food" (ECO-04) is a derived query (`NeedsSystem.NoWater/NoFood`): some living agent is below
  the threshold and no complete storage has unpromised stock. No extra state.
- ECO-07: `AgentSystem.ColonyLost` is set in `Kill` when no living agent is left and the event is emitted then, once.
  It is hashed and saved (in the agents section), so a load after the loss never re-emits it. Save `FormatVersion` 2.
- Starting stock (docs/00-overview.md): the Great Hall gets 40 berries, 30 water and 30 logs in `WorldFactory` after the
  river pre-settle and the colonists spawn.
- DSG-08 deadlock: two agents at their Dig steps, each standing on the other's block, waited 200 ticks and both failed.
  Now the one processed first stands down (no failure, JOB-08 cooldown; idle step-aside moves it off the marked floor).
  The M4 tree-floor pit test hit this once drink trips changed the timing.
A trapped agent whose need is also at 0 dies of that need (step 6 runs before the drowning damage at step 10).
Consequences: golden regenerated. With no commands seed 1 loses the colony at tick 15,012 (day 6): 30 water covers
three drink rounds. Long M4 scenario tests (8,000-20,000 ticks) now stock their hub with water and berries, and the
WAT-14 "water recedes" test allows the ECO-06 regeneration that resumes after the water drops (still no damage).

## ADR-044: Build and deconstruct tools, building look, top bar, colony-lost modal (2026-09-26, M5-T6)
Context: VIEW-09, 12, 14, 15 and 18 leave open where a ghost stands relative to the pick, what a click on a red
ghost does, how Build ▸ Warehouse / Pump / Levee is chosen from the keyboard, how a complete building is drawn when
its cells are already BuildingSolid terrain, and what the top bar shows before the weather system (ECO-17/18) exists.
Decision:
- All tool rules are in ViewCore (`Tools/BuildTool.cs`, `Tools/DeconstructTool.cs`), unit-tested; the Godot layer only
  forwards input, draws, and enqueues `PlaceBuilding` / `Deconstruct` (both existed since M5-T2).
- Build ghost origin = the empty cell in front of the picked face (`PickHit.Adjacent`), so on flat ground it stands on
  the picked block. Green/red and the reason come from the sim's read-only `BuildingSystem.CanPlace`; reasons are
  player text (`BuildTool.ReasonText`). The entrance cell is shown as a white floor tile.
- A click on a green ghost sends `PlaceBuilding`; on a red ghost it sends nothing and toasts the reason (no rejected
  commands in the log). Without Shift the tool then returns to Select; with Shift it stays, and dragging with Shift
  held places one more building at each new green origin (levee lines). Origins sent in one drag are remembered, so
  a cell is never sent twice before the tick applies it.
- B enters the build tool; B again cycles warehouse → pump → levee (buildable = not `prebuiltOnly`, content order).
  R rotates by 90°. The toolbar Build button is a menu of the same buildings.
- Deconstruct (X): the target is the building covering the picked cell (complete buildings are solid blocks), else
  the one covering the cell in front of the face (blueprints and sites stand on the ground). The view mirrors the
  BLD-09 refusals (hub, already deconstructing, building on top) to show a reason instead of sending a command that
  would be rejected. The tool stays active after a click, like the drag tools.
- Buildings (VIEW-09): one footprint box per building, inflated 0.02 per side so a complete building's box hides its
  own BuildingSolid faces and shows the palette color (`buildings.<id>`). Blueprint = unshaded `buildings.blueprint`
  at alpha 0.35; site = building color at alpha 0.6; complete and deconstructing = solid. Label + camera-facing
  progress bar: materials delivered/needed while any are missing, else build percent; "Tearing down n%". A complete
  pump with `NoWater` gets a red "NO WATER" billboard. Boxes are cut at the slice level and hidden when their bottom
  is above it.
- Top bar (VIEW-15): "Day n" (1-based for the player), speed ("Paused", "1x", "3x", "6x"), totals per item in content
  order summed from complete storage buildings (not `BuildingSystem.Totals`, which is empty after a load until the
  next tick), and red alerts "No food" / "No water" (`NeedsSystem`) and "Pump has no water" (any complete pump
  flagged). Season and days until it changes are left out until the weather system (M6-T4, shown in M6-T5).
- Colony lost (VIEW-18): modal on the `ColonyLost` event, and after any attach/load whose colony is already lost;
  a load that brings living dwarves back hides it. Buttons: Load quick save (F9 path) and Close (keep watching).
  `CommandRejected` events are toasted.
- Screenshot harness: new `--script build` (chop + warehouse + pump + levee line at the nearest valid sites around
  the hub) and a fixed pick override (`GameRoot.PickOverride`) that shows a red warehouse ghost on the hub roof with
  its tooltip, since picking is off in shots.
Consequences: no sim code or state changed; golden hashes unchanged. Found while writing the `build` script: on
seed 1 no valid pump site has water at its intake anywhere on the map (1,921 valid sites, 0 wet). The river banks
rise one level per cell, so a pump whose front overhangs the water has its entrance inside the next bank step
(`EntranceBlocked`). A player must first dig that step (one cell). M5-T7 (SurvivalScript through the pump) has to
dig the entrance notch before placing the pump.

## ADR-045: SurvivalScript lives in ViewCore as a timed command list; seed-1 pump notch at x=40 (2026-09-26, M5-T7)
Context: docs/testing.md puts `Scripts.SurvivalScript` "in the test project", but it is also used by
`run-headless.sh --script survival`, and the headless runner cannot reference the test project (ADR-036 left this
open). The script also needs commands at later ticks: the pump can only be placed once its entrance notch is dug
(ADR-044). The notch M5-T6 checked, `(39,18,79)`, is never dug by colonists: a berry bush stands on it, and a dig
under a plant waits until the plant is gone (DSG-03, M4-T15); bushes are not choppable.
Decision:
- `Aurvangar.ViewCore.Scripts.SurvivalScript` (pure C#, beside `ScreenshotScripts`): a fixed list of
  `(Tick, ICommand)` for seed 1. `EnqueueDue(sim)` enqueues the commands whose tick equals `sim.Clock.Tick`, so they
  are applied at the start of that tick and logged with it; callers call it before every `Tick()` (`Run` does both).
  Commands are fixed coordinates, not searched: the script is a replayable command log.
- Content through M5: tick 0 dig notch `(40,18,79)` + chop the 48x48 area around the Great Hall (same as
  `ScreenshotScripts`); tick 600 pump at `(40,18,80)` rotation 0 (intake 1024; the notch is dug at tick 485);
  tick 1200 warehouse at `(34,24,54)`; tick 1800 five levees `(33..37,23,76)` rotation 0 along the top of the bank
  between the hall and the river. M6-T6 appends the farm, hill dig, breach and levee repair.
- The headless runner accepts `--script survival` and enqueues due commands before each tick. The screenshot
  harness does not support it yet (it enqueues everything before tick 1).
Consequences: golden hashes now cover construction, pumping, hauling and needs (regenerated). With the script no
dwarf dies of thirst; the colony now starves after the 40 starting berries (first death tick 23,091, lost at
29,011) until farms (M6).

## ADR-046: Moisture map: surface, height window, algorithm; saved and hashed instead of recomputed on load (2026-09-26, M6-T1)
Context: ECO-15 says "the top-surface cell" without defining it for columns under water, overhangs or buildings, or
with no solid cell. SAV-01 and ADR-032 say moisture is not saved and is recomputed on load. But the map is only
recomputed in ticks that are a multiple of 50, so between recomputes it reflects older water. A load at, say, tick
1025 that recomputes would see the water at 1025, not at 1000. Once crops read the map (M6-T2), the loaded game
would diverge from the original, which breaks SAV-03.
Decision:
- A column's surface is its highest solid cell (any solid block, including BuildingSolid). Under a river that is the
  riverbed, so the water above it (surfaceY + 1) moistens it. A column with no solid cell is dry.
- A column is moist when a water cell with level >= 128 lies within Chebyshev radius 5 at
  `y in [surfaceY - 2, surfaceY + 1]`, where surfaceY is the target column's own surface.
- Algorithm (`Water/MoistureMap.cs`): one top-down pass over all layers builds a per-column bitmask of the heights
  that hold enough water (skipping groups of four empty cells) and the surface height (the search stops once every
  column has one). The masks are OR-dilated along x, then along z (a separable 11x11 box). A column is moist if its
  dilated mask has a bit inside its window. This is the 2D dilation ECO-15 asks for; the height window stays exact
  because the mask keeps every height.
- `Simulation.Tick` step 4 calls `Moisture.Tick(Clock.Tick)`, which recomputes when `tick % 50 == 0`, after the water
  step. The flags (one byte per column) are part of `StateHash` and are saved in a new `Moisture` section after
  plants (RLE, values checked to be 0/1). Load restores them and does not recompute. Save `FormatVersion` is now 3.
  docs/specs/save-load.md SAV-01 is updated.
- `SurfaceY(x, z)` is a public helper that reads the current world (for farms and tests); the surface found during
  the recompute is scratch, not state.
Consequences: SAV-03 holds exactly for saves taken between recomputes. The saved size grows by the RLE of 16,384
bytes (small; mostly long runs). Seed-1 golden hashes change (the hash now includes the map); no behavior changes
until crops read the map. v2 saves no longer load (SAV-04). Recompute on seed 1: ~0.9 ms median (Release; budget
3 ms, ECO-16).

## ADR-047: Farm tiles: column-top designation, FarmSystem at step 5, harvest chains its own delivery (2026-09-26, M6-T2)
Context: ECO-11 says `DesignateFarm(area)` marks "top-surface Grass/Dirt cells" but not whether the area is a box or
an XZ rectangle, whether Farmland left by a cancel can be designated again, or what happens to a tile that is dug or
built over. ECO-12/13 do not say when in the tick crops grow, and JOB-11 does not say what a harvest does when no
storage has room (ECO-14 says the harvest posts regardless of storage).
Decision:
- `DesignateFarm(X0, Z0, X1, Z1)` is an XZ rectangle like `DesignateChop`. In each column the tile is the column's
  top solid cell (`MoistureMap.SurfaceY`, the same cell the moisture flag describes). It qualifies when it is Grass,
  Dirt, or Farmland without a tile, the cell above is standable (PTH-01: air, headroom, no plant), and neither cell
  belongs to a building. The block becomes Farmland at once (not an agent action, like a designation). A rectangle
  entirely outside the world is rejected. Existing tiles are unchanged.
- `Farming/FarmSystem` owns the tiles (SortedDictionary by cell index; state Empty/Growing/Mature, Progress,
  DryTicks). It ticks at ARCH-01 step 5, right after `PlantSystem`: it drops tiles whose block is no longer Farmland or
  whose cell above is solid or part of a building; grows crops (moist: DryTicks = 0 and Progress + 1, Mature at 7200;
  dry: DryTicks + 1, withered to Empty at 2400); then keeps one Plant job (priority 30, Work 30) per Empty tile and one
  Harvest job (35, Work 20) per Mature tile. Unclaimed farm jobs whose tile is gone or in another state are withdrawn.
  A crop planted in tick T first grows in tick T + 1 and is Mature at the end of tick T + 7200.
- New `WorldActions.Plant` / `WorldActions.Harvest` (step kinds `Plant`, `Harvest`) change the crop; Harvest puts 3
  potatoes in the actor's carried stack (it needs room for them).
- JOB-11: when the Harvest step succeeds, the job appends `GoToBuilding + DeliverTo` to the nearest complete storage
  that accepts potatoes and has room for all 3 (Manhattan to entrance, ties by id) and swaps its tile cell reservation
  for that storage reservation (`JobBoard.SetReservations`), so the tile can be replanted while the potatoes travel.
  With no such storage it appends a `Drop` at the harvester's cell; `HaulSystem` moves the pile once room appears.
- ECO-14 overrides the JOB-05 table's "storage below target" for crops: Plant and Harvest jobs are posted by
  FarmSystem (not PlantSystem) whatever the storage level. JOB-11's "rather than dropping" is kept whenever a storage
  has room; the Drop fallback exists only because ECO-14 lets a harvest run with none.
- Job selection skips a Plant / Harvest job whose tile is no longer Empty / Mature (`FarmSystem.StillWanted`), e.g. a
  harvest job released mid-delivery by a need job; it is withdrawn at the next farm step.
- DSG-06: `CancelDesignation` removes Empty tiles in the box (Farmland stays) and cancels their Plant jobs, claimed or
  not. Tiles with a crop stay.
- Tiles are hashed and saved (new `Farms` section after moisture); save `FormatVersion` is 4.
Consequences: a tile covered by a building or a placed block is lost silently (its crop too). Crops do not block
walking or building placement. Crop numbers (7200, 2400, 3 potatoes, work ticks) are spec constants in `FarmSystem`,
like `LogsPerTree`; the produce item is `potato`. v3 saves no longer load (SAV-04).

## ADR-048: Berry bush harvest jobs; storage-out promises end at pickup (2026-09-26, M6-T3)
Context: ECO-10 gives the bush cycle and "Harvest posts only while the colony's total food in storage < 60", but not
what counts as food, how bush jobs differ from farm Harvest jobs (both `JobKind.Harvest`), or what happens to a posted
job when the stock rises. Seed 1 starts with 40 berries, so from tick 0 all five dwarves pick berries (Harvest 35 beats
Dig/Chop 25). That delayed the SurvivalScript's notch dig past the pump's tick and exposed a latent BLD-10 bug: a
`PickUpFromStorage` step kept its job's StorageOut reservation after the items had left, so a second job reserving the
building's remaining stock failed its pickup (seen as one failed levee Deliver job).
Decision:
- Food in storage = units (not food value) of items with a food value (berries, potatoes) stored in complete storage
  buildings (`BushHarvest.FoodInStorage`). Carried items, piles and pending deliveries do not count.
- Bushes: a harvest sets Berries 0 and RegrowTicks 1200; `PlantSystem.Tick` (ARCH-01 step 5) counts down and makes the
  bush ripe (2 berries) on the tick the countdown reaches 0, exactly 1200 ticks after the harvest tick.
- `Plants/BushHarvest.Sync` (end of the plant step): while food < 60, one Harvest job (35) per ripe bush:
  `GoTo(reach) → Work(20) → HarvestBush(plant id)` with a cell reservation on the bush base. An unclaimed bush job is
  withdrawn when food ≥ 60, the bush is not ripe, or the job already has delivery steps (released mid-delivery; its
  berries were dropped and are hauled). Selection skips such jobs (`BushHarvest.StillWanted`). Claimed jobs run on.
- Bush jobs are told apart by their third step being `HarvestBush` (`BushHarvest.Is`); FarmSystem ignores them.
- New `WorldActions.HarvestBush`; the berries go to storage in the same job via `FarmSystem.ChainDelivery` (JOB-11).
- BLD-10 fix: a successful `PickUpFromStorage` step uses up the matching StorageOut reservation
  (`JobBoard.UseStorageOut`, as Consume already did). A Deliver or pump-buffer Haul job released after its pickup is
  re-planned when its reservations differ from the ones it would be posted with (`Construction.Replan`,
  `Pumps.KeepHaul`), so it gets its StorageOut back and is not claimed without stock (sim-reviewer finding).
- Test/script updates caused by the earlier food gathering: `SurvivalScript` chops at the pump's tick (600) instead of
  tick 0 (chops tie with the notch dig and held it up to tick 1136; now it is dug at 416);
  `Seed1_FiveColonistsSpawnStandable` checks the idle spawn state before the first tick instead of after it; the
  `build` screenshot script completes by tick 1600 instead of 1200 (`SCRIPT=build` shots need `TICKS=1600`).
Consequences: no new state (Berries / RegrowTicks were already saved and hashed); no save version change. A ripe bush
nobody can reach keeps a job on the board (region filtering keeps agents from trying it). Every stock drop below 60
sends dwarves to the bushes ahead of digs and chops.

## ADR-049: Weather is a function of the tick; drought springs seep away (2026-09-26, M6-T4)
Context: ECO-17 cycles Wet(5 days) / Drought(2 days) and sets the source strength; WAT-09 says sources are "set to"
`Full * strength / 100`, and water.md scenario 6 (`DroughtDrainsRiver`) needs the seed-1 river to lose >= 70% within
one day of drought. ADR-011 made sources raise-only. With raise-only springs, strength 0 only stops inflow: the flat
bed (ADR-009, ADR-021) drains through the x=127 drains alone and the river still held 79% after the full 2-day
drought. `Seed1_RiverHoldsVolumeThroughDay10` (M3-T8) asserted +-10% every 100 ticks to day 10, which a working
drought cannot satisfy.
Decision:
- `Water/WeatherSystem` is static: the season is `tick % 7 days < 5 days ? Wet : Drought`, so there is no weather state
  to save or hash. The strength it sets is `WaterGrid.SourceStrength`, already saved and hashed. No FormatVersion bump.
- ARCH-01 step 2 writes the strength only on the first tick of a season (5 d, 7 d, 12 d, ...; not tick 0, where the
  default 100 is the Wet value) and emits `SeasonChanged(season)` for the view. A strength set by hand (tests,
  scenarios) stays until the next season change.
- `SeasonAt`, `TicksUntilChange`, `DaysUntilChange` (rounded up) are the ECO-18 readout API for M6-T5.
- WAT-09 is now literal: a source cell is set to the target both ways. Raising counts in `SourceAdded`; lowering
  (only below full strength) counts in `Drained`, so WAT-11 still balances. This amends ADR-011 (2). At strength 100
  nothing changes (a cell is never above Full between steps), so wet-season behavior and the golden hashes are
  unchanged. In drought the spring columns (every 16 cells, ADR-021) act as sinks: the seed-1 river is at 23% 200
  ticks into the drought and empty by ~800; it is back to 96% 400 ticks after the drought ends and 99% by 1,000.
- `Seed1_RiverHoldsVolumeThroughDay10` now checks +-10% in the Wet season only, skipping the first 600 ticks after a
  drought (refill): ticks 100..11,900 and 17,400..24,000 (186 samples, counted in the test). The drought itself is
  covered by `DroughtDrainsRiver`.
Consequences: every drought empties the river completely for about 1.5 days, so crops wither (2,400 dry ticks) and a
pump runs dry unless water is stored (the DoD's "colonies that stored water survive"). While the river drains and
refills, active water cells peak near 3,500 and regions rebuild often (the whole bed becomes walkable), so drought
ticks cost more (headless survival run: 4,400 ticks/s over 10 days vs ~9,000 before day 5; median tick 0.063 ms,
p95 1.0 ms; SIM-P1 is measured at day 5, M6-T7).

## ADR-050: Farm tool, crop and berry look, season readout (2026-09-26, M6-T5)
Context: VIEW-11 says only "farm = brown overlay", VIEW-12 lists a Farm (F) tool, VIEW-15 / ECO-18 ask for the season
and days left. Farms and bush ripeness emit no events, the moisture map has no event either, and crops grow only on
moist tiles, which the player cannot see. Moisture is not computed before the first tick.
Decision:
- Farm (F) is a drag tool like Chop: the XZ rectangle of the two picks sends `DesignateFarm`; the preview is the box
  of the picked cells in green. Its mouse label (`Tools/FarmTool`) says "Moist: crops grow here" / "Dry: crops will
  not grow here" for the hovered column, and "m of n columns moist" during a drag, from `sim.Moisture`.
- Farm overlay (VIEW-11): two raised furrows per tile in `designations.farm` in the designation mesh. Crops
  (`Entities/CropMesher`, own opaque shaded `CropRenderer`): a 2×2 cluster of stalks on the furrows whose height
  steps through 8 growth stages; green (`plants.crop`) on a moist tile, straw (`plants.cropDry`, new palette key) on
  a dry one; a mature crop gets potato-colored caps (`items.potato`). Empty tiles show furrows only. The renderer
  polls a signature (cell, state, stage, moisture per tile), so a remesh happens at most 8 times per crop.
- Ripe bushes (ECO-10) show 4 berry cubes (`plants.berries`); `PlantRenderer` now rebuilds on a signature that
  includes ripeness, not only the plant count. Two `PlantMesherTests` that counted bush quads as a bare cone were
  updated for the berries (the change is intended).
- Hover label on a farm tile: "Farm tile: waiting for planting", "Potatoes n% (moist)", "Potatoes n% (dry, withers
  in t ticks)", "Potatoes ready to harvest".
- Top bar (ECO-18): "Wet season, N days left" / "Drought, N day(s) left" from `WeatherSystem.SeasonAt` and
  `DaysUntilChange` (rounded up, so day 2 of the wet season shows 4), orange in a drought. `SeasonChanged` shows a
  toast. The top bar is now anchored top-right and grows to the left.
- Screenshot harness: script `farm` designates a 5×5 field on the nearest moist ground to the hub, found on a
  private `MoistureMap` built from the sim's world and water (read only; the sim's own map is empty until tick 0).
  Preset `farm` is a close-up of the farm tiles (hub if none). It is opt-in: the default shots stay the four gate
  presets (`ScreenshotPresets.DefaultShots`).
Consequences: no sim changes; golden unchanged. The view reads `sim.Farms`, `sim.Moisture`, `Plant.Berries` each frame
for signatures (a few hundred entries on seed 1).

## ADR-051: Survival script: farm, hill tunnel, breach under the river surface, levees on the breach (2026-09-26, M6-T6)
Context: docs/testing.md asks the SurvivalScript for a 6×6 farm, a dig into the hill, a bank breach at tick N and a
levee line at N+300, with all 5 dwarves alive at day 10 (needs-economy.md scenario 5). No coordinates or ticks are
given. On seed 1 the river's top water layer is y=17, and the dry ground next to it is at y ≥ 18, so a tunnel at
ground level can never flood. A levee's entrance must be standable (BLD-02), and every cell beside a breach is either
solid bank or water.
Decision (seed-1 coordinates, fixed like the rest of the script):
- Tick 2400: `DesignateFarm(62,67,67,72)`, the nearest 6×6 all-moist farmable field to the hall (the `FindFarm`
  ring search with size 6). The first crops are harvested before the day-5 drought.
- Tick 3000: `DesignateDig((84,17,56),(85,18,65))`, a two-wide, two-high tunnel north into the hill's south slope
  (x=85 is 20 cells from the hill center). Its floor is one below the river surface, so it is entered by a one-step
  descent from the bank top at (84..85,18,66). Rows z ≤ 61 are stone (22 stone, all hauled by tick ~6900). Tick 3000
  keeps `SurvivalScriptTests.Seed1_BuildsPumpWarehouseAndLevees_AllAccepted` (which runs to tick 3000 and expects no
  marks and no failed jobs) valid.
- Tick 7200 (N): `DesignateDig((84,17,66),(85,17,66))`, the two bank cells between the tunnel mouth and the river
  water at z=67. The tunnel floods to deep water; a dwarf standing in it flees out over the bank (WAT-14).
- Tick 7500 (N+300): a levee on each breach cell, rotation 0. The entrance is the tunnel cell north of it: standable
  (PTH-01 ignores water), so placement is valid while it is flooded. Builders use the Building goal (any walkable cell
  next to the footprint), which includes the dry bank-top cells (83,18,66) and (86,18,66). The levees are complete by
  tick ~7900; the tunnel keeps its water through the drought while the river outside is dry.
- The "levee line at N+300" of testing.md is read as "levees on the breach". The M5-T7 levee line at tick 1800 stays.
- `PendingAcceptanceTests.cs` held only the two SurvivalScenario placeholders; they moved to
  `Scenarios/SurvivalScenarioTests.cs` with bodies and the empty file was removed.
Consequences: golden hashes at 3000 and 6000 change (farm at 2400). Headless seed 1 with the script: all 5 alive at
day 10, 0 failed jobs, 105 potatoes, 22 stone stored. The headless `summary:` dig/chop counts are measured against
the marks present after tick 1, so for `--script survival` they cover only the tick-0 notch (1 cell) and no trees.

## ADR-052: Perf runs are serialized, GC-settled and opted out of EcoQoS; packed A* heap key (2026-09-26, M6-T7)
Context: PTH-P1 (p95 ≤ 1.5 ms) failed intermittently in `perf.sh` (p95 up to 1.6 ms) while the same test alone gave
p95 ~0.8 ms. The perf-auditor found two harness artifacts: the perf classes ran in parallel (xUnit default), and on
this hybrid CPU (i9-12900KF) Windows 11 intermittently schedules the windowless `testhost` on efficiency cores
(EcoQoS). Every slow run had all timed searches on E-cores; pinning to E-cores reproduced the slow numbers exactly.
The game runs as a foreground window and is not throttled.
Decision:
- All perf classes share one xUnit collection `Perf` with `DisableParallelization = true`.
- `PerfHelpers.SettleGc()` runs a full blocking GC before each timed section (after warmup in `Measure`, before the
  PTH-P1 and SIM-P1 loops), and once per process opts it out of power throttling
  (`SetProcessInformation(ProcessPowerThrottling, execution speed control on, state off)`, Windows only, no package).
- A* speedup (same paths, same tie-breaks, PTH-09): `PathHeap` packs `(f, h, index)` into one `ulong`
  (20/16/28 bits; ordering by the packed value is the lexicographic order), per-cell search state is one `Node[]`
  array of 16-byte structs, and a single-goal search calls the heuristic directly. `EnsureArrays` throws for a
  world whose cell count or maximum heuristic would not fit the key (128×64×128 uses 2^20 cells and h ≤ 1,920).
- No budget changes.
Consequences: in-suite PTH-P1 p95 ~0.60 ms (6/6 runs), SIM-P1 median ~2.3 ms. Seed-1 survival hash is unchanged
(`7344b913cc2909c9`); goldens unchanged. On a non-hybrid or non-Windows CI runner the QoS call is a no-op.

## ADR-053: DoD walkthrough evidence: cumulative headless counts, a reservoir-pump scenario, a mid-session save (2026-09-26, M6-T8)
Context: M6-T8 maps each DoD step (docs/00-overview.md) to evidence. Three gaps: (1) the headless `summary:` measured
dig/chop work against the marks present after tick 1, so `--script survival` reported 1 cell dug and 0 trees felled;
(2) DoD 8 ends "colonies that built a levee reservoir keep their pump running", but the survival script's pump draws
from the open river and goes NoWater for the whole drought (the colony lives on stored water), and no test built a
levee reservoir; (3) DoD 10 (save, quit, load, continue) was tested on small setups and on a day-5 save's hash, not
on a save taken mid-session and played on.
Decision:
- Headless: a `WorkTracker` observes the sim after every tick (outside the timed section): a dig mark that vanishes
  over a non-solid cell counts as dug, a marked tree that leaves the plant list as felled, a farm tile going
  Growing → Empty as withered (ECO-13) and Mature → Empty as harvested; it also counts ticks with a NoWater pump by
  season. A `colony:` line per report shows season, complete buildings, pump NoWater, crops and storage. Sim output
  and hashes are unchanged.
- DoD 8 reservoir: a synthetic scenario (`ReservoirScenarioTests`, 32³ world, real weather cycle) instead of changing
  the survival script, which would need a new pump site and a golden regeneration for a step the colony already
  survives. With two levees across the mouth of a side pool the pump never flags NoWater through the drought and
  keeps pumping while the river bed beside it is dry; the control without levees runs dry.
- DoD 10: `SurvivalScenarioTests.Seed1_SurvivalScript_SaveLoadMidSession_ContinuesIdentically` saves at tick 7,400
  (flooded tunnel, repair not yet ordered), loads, and runs both games with the rest of the script to tick 12,600.
Consequences: evidence for every DoD step is a test or a headless stat. check.sh gains ~15 s (Debug) for the save
test. The survival session itself still has no reservoir pump; G3 may ask for one in the script.


## ADR-054: Right-drag orbits like middle-drag; right click vs drag by a 4 px threshold (2026-09-26, M7-T1)
Context: G3 play feedback: "I find I am right-clicking to pan and keep forgetting it is center mouse." M7-T1 asks
that right-drag "pans like middle-drag (keep middle-drag)". Middle-drag orbits (yaw and pitch, ADR-019); the only
translating pan is WASD. A right press also aborted the tool drag in progress (ADR-033), so a right-drag would have
killed a dig box being dragged with the left button.
Decision: right-drag does exactly what middle-drag does (`OrbitRig.Drag`): the human named the middle button as the
control they were reaching for, so "pan" here means the middle-drag camera move. `ViewCore.Camera.ClickDragGesture`
tells a click from a drag: a press becomes a drag once the net pointer displacement since the press is more than
4 px; the crossing motion hands over the whole accumulated delta (no lost pixels), later motion passes through. A
release before the threshold is a click and aborts the tool drag (`CameraRig.RightClicked` → `ToolController.AbortDrag`);
the abort moved from right press to right click release. A right-drag leaves a left-button tool drag alive. Middle-drag
keeps orbiting from the first pixel (no threshold). No HUD controls text exists to update.
Consequences: a right click aborts on release instead of press (imperceptible). If the human meant a grab-the-map
translation, `ClickDragGesture` can feed a new `OrbitRig` pan method instead of `Drag` with no other change.


## ADR-055: A pump may use the stand cell one level above its entrance (2026-09-26, M7-T2)
Context: G3 answer 3b. On seed 1 the river banks rise one level per cell, so a pump whose intake is over the water
has its entrance inside the next bank step (`EntranceBlocked`); 0 of the valid pump sites were wet and a player had to
dig a notch first (ADR-044). The human chose "the pump may use a stand cell one level up, the way levees stack".
Decision:
- The rule applies to `waterEdge` buildings (the pump) only. Other buildings keep BLD-02; a warehouse whose entrance is
  blocked stays red. `BuildingSystem.RaisesStand(def)` is the one switch.
- Placement (BLD-03): the entrance passes when it is free (standable, no building), or for a stacked building the cell
  below is (ADR-040), or for a `waterEdge` building the cell above is. The footprint stays in reach from there
  (ARCH-07, 26 neighbours: dy = -1 and one step across). The cell above a pump's entrance is reserved like an
  entrance: no footprint may cover it (`Overlaps`), whether or not the pump uses it.
- Stand cell (`Construction.StandCell`, dynamic from the current grid): the entrance if standable, else the cell below
  if standable (stacked levees), else for a `waterEdge` building the cell above if standable, else the entrance. The
  OperatePump job's `GoTo(Exact)` and its target now use the stand cell; an unclaimed job whose stand cell moved (the
  bank step was dug later) is cancelled and posted anew. Refunds, agents moved out of a site, and the build ghost's
  white stand tile (`BuildingSystem.PlannedStandCell`) use the same cell.
- The SurvivalScript keeps its tick-0 notch (the task allows it): with it dug the entrance is standable, so the
  script's pump behaves exactly as before and the golden hashes do not change.
- The screenshot `build` script now puts its pump at the nearest wet site (falling back to the nearest valid site), so
  the shots show a working pump instead of a NoWater one.
Consequences: on seed 1 (pre-settled river, no digging) 354 wet pump sites with a stand cell in the Great Hall's region
exist, all using the raised cell; the nearest is the survival script's own site (40,18,80). No new state, no save or
hash change. The stand cell is not stored, so if the bank step is dug the pump's worker simply moves down to the
entrance.


## ADR-056: Survival reservoir: a dammed trench beside the farm, filled on day 1, sealed before the drought (2026-09-26, M7-T3)
Context: G3 answer 4 / M7-T3. In the survival session the pump drew straight from the river and the farm relied on the
river for moisture, so the drought (ADR-049) left the pump dry for ~4,700 ticks and withered 41 crops. The task asks
for a levee-held reservoir that carries the pump (dry ticks "near 0") and one field (a second harvest before day 10).
Decision:
- Reservoir: a one-wide trench at x = 67, z 61..70, dug from the bank top down to y = 17 (floor y = 16, water layer
  y = 17, the river surface). Its pump end (67,17..18,70) and the pump pad (68,18,70) are dug at tick 0, the rest at
  tick 600. The bank cell (67,17,71) is a dam until tick 1200 (the trench is dug by ~tick 900), then dug so the river
  fills the trench (~1,000 per cell), and at tick 9600 a levee (rotation 0, entrance in the trench) seals it. It is
  complete at ~tick 11,700, before the drought (tick 12,000).
- Pump: moved from (40,18,80) to the pad (68,18,70), rotation 90, with its intake in the trench at (67,17,70). Its entrance
  (69,18,70) is bank, so the worker uses the raised stand (69,19,70) (ADR-055). The tick-0 notch of ADR-044 is dropped.
- Farm: the old 6x6 field (62,67)..(67,72) lost its east column (x = 67 is the reservoir): 5x6 = 30 tiles, all within
  5 columns of the held water (ECO-15). Moisture already counts water >= 128, so no sim change was needed.
- "Pump dry ticks in the drought near 0" is tested as <= 30 (one pump cycle); measured 0. "A second harvest before
  day 10" is tested strictly: every one of the 30 tiles is harvested at least twice, the second after day 5.
- `SurvivalScriptTests` day-1 hub water assertion drops from > 50 to > 30: the pump now starts at ~1,300 (after the fill),
  not ~800, so less water is stored by tick 3000 (measured 36). The intent (the pump delivers water on day 1) holds.
- The generic `ReservoirScenarioTests` field control asserts that the field beside the open pool dries out (every tile
  dry at drought end), not that it withers: the 20-wide pool drains through its 2-wide mouth slowly, so the dry spell
  (~460 ticks per tile) is shorter than the 2,400 ticks a crop needs to wither (ECO-13).
Consequences: golden hashes after tick 0 change (regenerated). In the 24,000-tick headless session: 60 crops harvested
(was 35), 0 withered (was 41), pump dry ticks 855 (all before the fill on day 1; 0 in the drought, was 4,712).

## ADR-057: Need jobs take the most plentiful restoring item across reachable storages (2026-09-26, M7-T4)
Context: G3 answer 6 / M7-T4. Eat/Drink jobs took the lowest-id food in the nearest storage, so berries were always
eaten first and potatoes piled up (180 at the end of the survival session). The answer: eat the most plentiful food
first, "most units in reachable storage (ties by item id)".
Decision:
- "Units" are unpromised units (`JobBoard.StorageStock`), not food value, summed per item over every complete storage in
  the agent's region (PTH-13 reachability, as before). Highest total wins; a tie goes to the lower item id (berries).
- The job then goes to the nearest reachable storage holding that item (Manhattan to entrance, ties by lower building
  id, as before), even if a nearer storage holds only the less plentiful food. The rule is about which food, not
  which building.
- The same rule serves Drink (only water today), so drink behaviour is unchanged.
- No new state; the choice is computed at post time.
Consequences: in the survival session berries and potatoes are eaten in turn, keeping the two stocks level. Bush harvest
still posts while berries + potatoes < 60 (ADR-048), so with potatoes plentiful the bushes rest more. Golden hashes
move (regenerated).
Test fix: `FarmScenarioTests.Harvest_StorageFull_DropsPile` fills the hub with 100 potatoes; its dwarf gets hungry
during the 7,200-tick growth and now ate 2 of them (100 potatoes beat 30 berries), so the hub was no longer full. The
helper now also tops berries up to the cap (a 100/100 tie eats berries), which restores the test's premise; the
assertions are unchanged.

## ADR-058: Give-up marks: 3 strikes per recurring job source, region checks, reset nearby or on a new storage (2026-09-26, M7-T5)
Context: G3 answer 7 / M7-T5 asks for a give-up mark: a recurring haul, delivery or pump job that is "reposted N times"
without ever succeeding is marked unreachable. It stops reposting until the world changes near it (a walkability
change or a new storage), and the HUD shows a notice. ADR-030 left jobs cancelled at five failures to be reposted
forever. The task does not say what N is, what counts as a repost, or what "near" means. It also misses one case: a
job the PTH-13 region filter keeps from every agent is never claimed. It never fails, so it is never reposted. That
is the usual way a job can never succeed (the task's own scenario is an unreachable pump entrance).
Decision:
- Sources (`GiveUpSource`): a construction site's deliveries (building id), a pump's OperatePump job (pump id), a
  pump's buffer haul (pump id) and a loose pile's haul (flat cell index). Other jobs (digs and chops already have
  JOB-08 designation marks; farm, need and construction-work jobs) are not tracked.
- A strike is either (a) a JOB-08 cancellation at the fifth failure, which is the "repost", or (b) a check every
  50 ticks (the retry cooldown) that finds the source's open job reachable from no living agent's region. Case (b) is
  one strike per source per check and runs no A*. N = 3 strikes: 15 failures, or about 100 ticks of being
  unreachable (checks at ticks 0, 50 and 100).
- A given-up source's poster withdraws its open jobs and posts none. A claimed job runs to its end.
- A mark is removed (strikes and all) when:
  - a job of the source completes;
  - a walkability change lands within Chebyshev distance 8 of the mark's cell. The mark's cell is the job target at
    the last strike. A walkability change is a block change, a plant occupancy change, a construction-site change, or
    a deep/shallow crossing in a standable cell. `PathGrid.WalkChanges` records these; it is cleared every tick;
  - a storage building is completed (every mark goes);
  - the source is gone;
  - (sim-reviewer) a mark whose last strike came from the region check is removed when its cell is back in a living
    agent's region. A given-up source posts no job to check, so without this rule a far-away reconnection (a flood
    draining 20 cells away) would leave it given up for good.
- Strikes stop at 3. A job claimed before the give-up may still fail later.
- `SaveGame.AfterLoad` and `WorldFactory` clear `WalkChanges`. Restoring plants and construction sites fills the
  list, and it must not reset marks: the sim-reviewer found this save/load hash split.
- Format version 5 means v4 saves no longer load (SAV-04, no migration).
- `JobGiveUp.Tick` runs in ARCH-01 step 11, right after the region rebuild, so checks see this tick's regions.
- The HUD alert "Unreachable: Water Pump, log pile x2" is built in ViewCore (`TopBarModel.GiveUpText`). The Godot top
  bar already shows every alert, so the Godot code is unchanged.
- The marks are sim state: saved (new `GiveUps` section, format version 5) and hashed. They are hashed only when there
  is at least one mark, so a game that never strikes hashes as before. The seed-1 goldens and the survival session
  hash are unchanged.
Consequences:
- Water-depth changes count as walkability changes, so a pump on a fluctuating bank may be reset and given up again
  every ~100 ticks while the water moves. Its notice then flickers.
- The reset is local: opening a far-away passage that would connect the regions does not reset a mark. A storage
  completion, or any change within 8 cells, does.
- A partly delivered site that is given up keeps what it has.
- A source whose own cell is reachable but whose other leg is not (a pile haul to a storage in another region) is
  recovered by the region check and struck again. Its notice can come and go about every 200 ticks. This is cheap
  (no A*, no failures) and accepted.

## ADR-059: The strand rule protects every dwarf: one cached cut set per dig, idle dwarves step out (2026-09-26, M7-T6)
Context: G3 answer 8. ADR-037 only kept the digger's own stand cell connected to the Great Hall; another dwarf working
inside a pit could be cut off by a dig made from outside. The backlog asks for one region check per candidate dig.
Decision:
- A dig also waits while it would cut any other living dwarf off from the hall: a dwarf whose cell (or, mid-step, the
  cell it steps into) reaches the hall now and would not after the dig. Dwarves already apart from the hall do not
  count. The digger itself stays under the ADR-037 stand-cell rule (its current cell does not count, since it walks
  to a safe stand cell).
- Checked where ADR-037 checks the digger: job selection (`JobRunner.Select` skips the dig, last filter, so only for a
  candidate that would win), Work start and the Dig step (stand down: back to the board with the JOB-08 cooldown, no
  failure). DSG-09's give-up is unchanged: a dig held up only by another dwarf still has a safe stand cell, so it is
  never turned red for that reason; it waits.
- One check per dig: `DigTrial.MayCut` computes, per dug block, the cells the dig cuts off, and caches it until
  `WalkabilityVersion`/`DeepVersion` move (with the `MaySplit` cache) or the hall changes. Only digs whose local test
  (`MaySplit`) says they may split pay for it. It floods from each move neighbor of the cell on top of the block
  against the hall anchors on the what-if view (neighbors met by an earlier flood share its answer). A side that runs
  out is a cut-off pocket. If the hall's side runs out first, its component is kept instead and every cell outside it
  counts as cut. Each dwarf is then a set lookup plus a region compare (a live flood only when regions are stale).
- Deadlock guard: two dwarves in the same pocket each hold the dig up for the other. An idle dwarf with nothing to do
  whose cell an open, unclaimed dig would cut off walks towards the hall (`DigStrand.StepOut`, before the ADR-029
  step-aside). A dwarf in the pocket can itself take the dig (it is not "another dwarf" for its own dig) and does it
  from a stand cell outside the pocket.
- Deconstruction (`Construction.WouldStrand`) keeps the digger-only rule; M8 construction reuses the strand rule for
  walling-in and will extend it there.
- Derived only: no new saved or hashed state. The cache is a pure function of the world, so a loaded game answers the
  same.
Consequences: seed-1 goldens and the survival headless hash are unchanged (no dwarf is ever inside a pocket when a
cutting dig is taken there). A dig may lose its Work progress when another dwarf walks into its pocket before the Dig
step; it stands down and is re-taken later.

## ADR-060: Readable labels: the mouse label avoids billboards, building labels declutter; timed screenshot scripts (2026-09-27, M7-T7)
Context: G3 found the harness's build-ghost tooltip over the pump's NO WATER billboard (build1600), and the survival
session could not be screenshotted because the harness enqueued every script command before tick 1. Since M7-T2 the
build-script pump stands on a wet site away from the hall, so build1600 no longer shows that overlap, but nothing
prevented it.
Decision:
- The mouse label (build/deconstruct tooltips, pile counts, farm hints) is placed by `ViewCore.Hud.LabelLayout.PlaceTooltip`:
  below-right of the point (as before) unless that covers a building billboard (name/progress label, progress bar,
  NO WATER), then above-right, below-left, above-left, then just below or above each billboard in the way, nearest
  to the default first, always on screen. With no clear spot it takes the least overlap. Pile count labels are not
  obstacles (they are small and the pile label is what the mouse label shows when hovering a pile).
- Billboard rectangles are computed on the Godot side from the Label3D's font size, its pixel offset and the
  fixed-size scale `pixelSize * viewportHeight / (2 tan(fov/2))` (`LabelLayout.BillboardScale`, independent of
  distance). Checked against rendered labels to within a few pixels.
- Building labels that overlap each other on screen (e.g. the two breach levee sites) are lifted apart each frame
  (`LabelLayout.Declutter`): the lowest label on screen stays, each later one moves up just above the one it hits.
  The lift is a Label3D pixel offset; the progress bar stays on its building.
- `--script survival` in the harness is timed: `ScreenshotScripts.Run` enqueues each `SurvivalScript` command just
  before its tick (`SurvivalScript.EnqueueDue`), the same as the golden, scenario and headless runs, so a harness run
  reaches the same state hash. Untimed scripts still enqueue everything before tick 1.
- Two presets for the survival session (fixed seed-1 cells from `SurvivalScript`): `tunnel` (the hill tunnel and
  breach, sliced at the tunnel's headroom y = 18, camera from the east) and `reservoir` (the reservoir trench and the
  farm beside it, camera from the south over the river). Suggested ticks: 7700 flooded tunnel with the breach levee
  sites, 8400 levees complete, 3000 the full reservoir, 14400 the drought (river empty, reservoir and tunnel still
  hold water).
Consequences: view-only; no sim change, no save or hash change. The harness runs survival shots in a few seconds
(14,400 ticks). The headless tool's script list now comes straight from `ScreenshotScripts.Names`.

## ADR-061: Free-form block construction amends ADR-001: construction blocks, plan entries, support and build rules (2026-09-27, M8-T1)
Context: G3 play feedback ("not much for me to do ... I want to plan monuments"). The human chose free-form block
building: paint structure block by block, and dwarves haul the material and build it. ADR-001 allowed prefab
buildings only. M8-T1 asks for a spec (`docs/specs/construction.md`, CON-01..18) sized for a POC.
Decision:
- **ADR-001 is amended.** Prefab buildings stay for functional buildings (hall, warehouse, pump, levee). Structure is
  free-form, from construction blocks. Control stays indirect: the player designates, dwarves build through
  `WorldActions.PlaceBlock`.
- **Blocks.** There are three construction block types, ids 8..10, as new `BlockId` values: `Masonry` (1 stone),
  `Planks` (1 log) and `PolishedStone` (2 stone, decorative).
  - They are ordinary block bytes, so the world, water, paths, mesher, save and hash need no per-cell side state. A
    block is "built" because of its type.
  - Natural stone stays `Stone` (terrain). A player cannot build terrain types.
  - `blocks.json` gets optional `label`, `cost` and `buildTicks` fields. `ContentDb` now also checks that no json id
    lacks an enum value.
  - The cost is one item type of at most 10, so a carried stack and a refund pile always hold it.
- **Plan entries** are one store with two states: `Planned` (the plan layer, M8-T4) and `Released` (built by
  dwarves).
  - They are saved from M8-T2 with the state byte, so there is one format bump (v6).
  - They are hashed only when present, so the goldens stay put.
  - Statuses (why an entry waits) are derived, never stored.
- **Shapes** are axis-aligned only: Single, Line, Wall, Floor, HollowBox (an open-topped ring) and Stair.
  - `Stair` is added beyond the task's list. Dwarves climb only 1-block steps, and reach is +-1 level, so a wall
    taller than 2 needs a way up. The plan provides one; there is no scaffolding.
  - A staircase built of blocks is not the out-of-scope "stairs and ladders" feature, which means new movement
    rules; the overview now says so.
  - Diagonal lines are left out to keep shapes trivial and deterministic.
- **Support.** A built block needs a solid block below it or beside it. Ground is terrain, Bedrock and BuildingSolid.
  "Grounded" means a path down or sideways through built blocks to ground; nothing hangs from above.
  - Placement checks only the local rule. That is enough, because every solid neighbour is already ground or
    grounded (the invariant).
  - Removal (any dig, or a building deconstruction) waits or is rejected when it would unground a built block. That
    is checked by a capped BFS from the removed block's upper and side neighbours.
  - Terrain never collapses.
  - Overhangs and bridges of any length are allowed. This is simple, and monuments want arches.
- **Order.** Bottom-up by a local rule: an entry waits while the cell below it has an entry. Reachable-first comes
  from posting only `Ready` entries (a stand cell in a living dwarf's region, material reachable) plus the JOB-06
  distance tie-break.
- **Build jobs** are trips, not single blocks.
  - One job fetches up to 10 units from storage (ADR-041 source rule; piles are not a source) and places up to
    `10 / cost` nearby entries.
  - Without batching, a 300-block monument would cost one storage round trip per block, too slow to finish by
    day 10 (M8-T6).
  - Priority 25, equal to Dig and Chop.
- **No walling in.** This is the ADR-059 rule with a new what-if view: placing a block removes a cell and headroom,
  where digging removes a floor.
  - The code mirrors `DigTrial`/`DigStrand` (`PlaceTrial`/`PlaceStrand`: a local `MaySplit`, then a cached cut set)
    so the cost profile is the same.
  - Only dwarves are protected. Sealing an empty room is allowed.
- **Water.** A built block is just a solid, so a built wall holds water like a levee (WAT-12 push on placement).
- **Refund.** Digging a built block refunds 100% of its cost (BLD-09 refunds 50%), so redesigning is cheap.
  `DesignateDeconstructBlocks` marks only built blocks, so a drag over a monument does not dig its ground.
- **Give-up.** Build jobs are recurring, so they get a JOB-12 source (`GiveUpSource.Build`, keyed by the seed cell),
  as ADR-058 requires for new recurring job kinds.
- **Placeholder tests** for M8-T2..T6 are in `tests/Aurvangar.Sim.Tests/PendingAcceptanceTests.cs`.
Consequences:
- A tall structure needs a way up planned into it (a stair or a stepped design). If none is planned, its upper
  entries wait with status `NoAccess` or `WouldStrand`. The HUD and ghosts show that (VIEW-22).
- Entries blocked by a pile wait until the pile is hauled. Entries on cells that hold a plant are rejected when
  designated.
- The existing `PlaceBlock` natural-block path (free, no support check) is kept for tests only.
- `ContentDbTests` asserts 8 block types; M8-T2 changes it to 11.
- The seed-1 goldens should be unchanged by M8-T2..T5, since no plan exists in `SurvivalScript`.

## ADR-062: Build-block jobs: implementation choices (2026-09-27, M8-T2)
Context: M8-T2 implements CON-01..05, CON-07..09 and CON-11..16 (ADR-061). A few points were open or did not work
as written.
Decision:
- **Folder and namespace `Blocks`, not `Construction`.** `Aurvangar.Sim.Construction` would shadow the existing
  `Aurvangar.Sim.Buildings.Construction` static class inside every `Aurvangar.Sim.*` file. `BlockPlans`,
  `BlockBuildSystem`, `BuildShapes` and `Support` live in `src/Aurvangar.Sim/Blocks/`. CON-04 now says so.
- **Shared what-if flood.** `TrialFlood` (the two-sided generation flood) and `TrialCutOff` (the cut set) were pulled
  out of `DigTrial`; `DigTrial` and the new `PlaceTrial` both use them, so dig and place strand checks cost the
  same. `PathGrid.FlagsIfSolid` gives a column's flags with one cell made solid.
- **Re-check re-plans.** CON-12's re-check cancels an unclaimed Build job only when its seed is gone or given up, or
  a remaining cell is not `Ready`. Otherwise cells already placed leave the job and its source and count are
  re-planned, so a job that failed after its pickup keeps its JOB-08 failure count (cancel-and-repost would reset it
  and fifth-failure give-up could never trigger).
- **Build give-up marks and the region "back" recovery.** `JobGiveUp.StrikeUnreachable`'s recovery (a mark whose cell
  is reachable again is dropped) skips Build marks: a Build seed's own cell is often reachable while the material
  is not (CON-15's typical case), which would drop and re-add the mark forever. Build marks clear through the other
  JOB-12 resets (a change within the reset radius, a new storage, a completed job) or when the entry is gone.
- **Material check.** CON-05 check 9 (`NoMaterial`) needs a storage serving a stand cell's region with at least one
  block's cost unpromised (`StorageStock >= cost`).
- **Step-aside and step-out scan entries.** An idle dwarf steps aside from any released entry's cell or the cell
  below one (not only `Ready` ones: an entry is `Occupied` exactly because a dwarf stands there). `StepOut` scans
  released entries in ascending cell order rather than jobs, because an entry that would wall a dwarf in is
  `WouldStrand` and never gets a job.
- **Strand anchors.** The hall's anchors are not filtered; P and P + down are simply not walkable on the what-if view,
  so the flood never seeds from them. Same effect as CON-14's wording.
- **Re-goal.** A GoTo(Build) fixes its goal stand cell when the move starts. When another builder's block goes into
  that cell the PTH-16 repath fails; instead of failing the job, the step restarts (the cell is re-checked and fresh
  stand cells are picked). It only triggers when the goal cell stopped being walkable, so it cannot loop without a
  world change.
- **Test worlds.** A free-standing wall's third course is out of reach from the ground (reach is one level up). The
  wall scenarios therefore give the builders a way up, as ADR-061 requires of plans: scenario 1 has a one-high ledge
  along the wall, and scenario 5 is a 3-long, 5-high wall with a 4-step stair against it (every stair step rests on
  the wall beside it).
- **Review fixes.** A Build job whose cells were all skipped does not count as a success for its give-up mark
  (only one that placed a block clears it, CON-15). Plan support over the flood cap counts as unsupported, as CON-09
  says. The BlockPlans save section must list cells in strictly ascending order.
Consequences:
- `ContentDbTests` asserts 11 block types.
- Save format v6; v5 files are refused (SAV-04). The seed-1 goldens are unchanged: an empty plan store adds nothing to
  the hash.

## ADR-063: Deconstructing built blocks: implementation choices (2026-09-27, M8-T3)
Context: M8-T3 implements CON-10, CON-17 and CON-18. A few points were open.
Decision:
- **One support test, `Support.Depends`.** It takes a set of removed cells: one cell for a dig, the whole footprint
  for a building (cells of the same footprint do not hold each other up). Each built up/side neighbour gets its own
  grounding search, depth-first with the down step tried first, capped at 4096 visited blocks (over the cap =
  depends, CON-10). A cell with no built neighbour answers after 5 lookups, so terrain digs pay almost nothing.
- **Where it applies.** `DesignationSystem` posts no job for such a mark (it waits and never turns red);
  `JobRunner.Select` skips an already-posted one; Work start and the Dig step stand down with the JOB-08 cooldown and
  no failure, as the strand rule does. `WorldActions.Dig` returns `Blocked`.
- **Teardown guard.** The `Deconstruct` command rejects `SupportsBlocks` only for a complete building. A block can
  still be placed against a building that is already being deconstructed (its BuildingSolid is ground for placement
  support). The last Deconstruct tick is then `Blocked` in `WorldActions` and the job stands down without a failure
  until the block is gone, so the CON-09 invariant holds on every path.
- **`NothingToDeconstruct`** is also the reason for a box wholly outside the world (DesignateDig says "box is outside
  the world"); the command has one rejection reason.
- **Refund pile** goes on the dug cell like a natural drop. Piles left in mid-air by digging under them are an
  existing behaviour (natural digs do it too); they are still hauled from any stand cell in reach.
- **Test invariant helper.** `Tests/Support/Grounding.Floating` checks CON-09 with its own flood (up/sideways from
  blocks resting on ground), independent of `Support`, for reuse in M8-T6.
Consequences:
- A tall single-column pillar comes down strictly top-first and a cantilever from its free end. A wide structure is
  only biased top-down (DSG-04): a lower block goes whenever its neighbours stay grounded without it.
- Seed-1 goldens are unchanged (no built blocks in the survival session).

## ADR-064: Plan layer: implementation choices (2026-09-27, M8-T4)
Context: M8-T4 implements CON-06 and CON-07 `ReleasePlan`. The `Planned` state, its save byte and hash, and the
poster ignoring it already came with M8-T2. A few details were open.
Decision:
- **Both are O(entries).** `ReleasePlan` walks the plan entries and tests each against the box; it never walks the
  box's cells, so a world-sized box costs the same as a small one. `Needed` sums over the entries on every call, as
  CON-06 says (no cache). Both scale to a monument plan of thousands of entries.
- **The box** is inclusive, and its corners may be given in any order. Out-of-world corners are allowed. Only
  `Planned` entries count, so a box holding only Released entries is rejected with `NothingToRelease`.
- **Released entries are not re-checked.** Release changes the state and nothing else. An entry whose support or
  access changed since it was planned just shows its CON-05 status (for example `BelowFirst` when the entry under it
  is still Planned, `NoSupport` when its support was dug away). The player sees why it waits in M8-T5.
- **`Needed`** returns `(ItemId, Count)` pairs in ascending item id and leaves out items with a zero sum.
  `BlockPlans` now takes the `ContentDb` in its constructor to read block costs.
Consequences:
- No save format or hash change: the new command is only a `CommandCodec` entry (tag `ReleasePlan`, two Int3).
- Seed-1 goldens are unchanged.

## ADR-065: Block build tool, plan view and material totals: implementation choices (2026-09-27, M8-T5)
Context: M8-T5 builds the view side of construction (VIEW-21..23). The spec leaves several details open, and the
ghosts and HUD need the plan statuses often enough that a per-cell query would be too slow.
Decision:
- **Two batched sim queries.** `BlockPlans.CanPlanAll(sim, cells)` gives the same result per cell as
  `CanPlan(sim, cell, cells)` but runs one support flood. `BlockPlans.Statuses(sim, maxY)` gives every entry's CON-05
  status from one `BuildScan` and stops above `maxY` (cells are y-major). Both are read-only, so there is no state,
  save or hash change.
- **Refresh rates.** The tool ghost is rechecked when the command changes or every 5 ticks. Plan ghosts rebuild when
  the entries or the slice change, or every 10 ticks while entries exist. The plan line in the top bar rebuilds every
  10 frames and after a remesh flush.
- **Clicks.** A block drag with no valid cell sends no command and shows the reason as a toast. Shift keeps the tool,
  as BuildTool does. Invalid cells are skipped by the sim (CON-08), so a partly red drag still builds the green cells.
- **Height.** With the slice active, the start height reaches the slice (`SliceY - A.Y + 1`, clamped to 1..32);
  without it, the start height is 3. +/- or Ctrl + wheel sets an override, kept until the tool is changed or reset.
  The camera ignores Ctrl + wheel.
- **Release (L) and Deconstruct box.** Both drag an XZ rectangle. The box runs from the lower pick's Y up to the
  slice level, and the preview draws only its footprint. A Deconstruct press on a building still deconstructs the
  building; elsewhere it starts the block drag. "Release all" is `ReleasePlan((0,0,0), world max)`.
- **Hover** reads the entry in the cell the picked face looks into (`hover.Adjacent`), so an entry is found by
  pointing at the face under or beside it as well as at the ghost.
- **Material line.** "Building" compares the released need with stock. "Planned" is flagged short when the released
  plus planned need exceeds stock, since the released blocks take the stock first. Stock is `TopBarModel.Totals`,
  which is right after a load.
- **Stuck ghosts** (GivenUp, NoAccess, WouldStrand, NoSupport) are tinted red. Waiting states (BelowFirst, NoMaterial,
  Occupied) keep the block colour.
- **Layout.** The block options row (block, shapes, Plan, height, Release all) sits at the bottom left, clear of the
  colonist panel and the top bar.
- **Screenshots.** A new `blocks` script and preset. The script builds a planks wall, a released Stone wall and a
  planned Polished stone box on a flat site next to the hub. The harness holds a Stone wall drag across the planks.
Consequences:
- Seed-1 goldens and the survival hash are unchanged.
- On seed 1 the Stone wall does not get built in the `blocks` shot: the digchop pit is 2 deep, and stone starts 5
  below the surface (GEN-04). The top bar shows this as a short "stone 18/0", which is useful to see.

## ADR-066: Monument session: buffer haul priority, reachable stand cells, course-by-course release (2026-09-27, M8-T6)
Context: M8-T6 needs a scripted session on seed 1 that quarries stone and raises a hollow 7×7, 8-high tower with a
door and an inner stair, plus a courtyard wall, by day 10 with all 5 dwarves alive. Early versions of the script
showed two sim problems and a weak spot in batching:
- With many Dig and Build jobs open (priority 25), the pump buffer's Haul (20) waited behind them. The pump stopped
  at a full buffer, the hub ran dry, and in some script versions the colony died of thirst.
- `JobGoals.BuildStandCells` preferred free stand cells even when no dwarf could reach them. The top of a
  half-built courtyard wall was such a cell: the Build job's only goal was unreachable, JOB-12 struck it three
  times and gave up the cell (seen at (63,26,45)), and every cell above it waited forever.
- CON-12 posts a job as soon as one cell is Ready. When a whole plan is released at once, cells turn Ready one at a
  time as the courses below finish, and batches averaged about 1.3 blocks, so dwarves walked to the warehouse for
  almost every block.
Decision:
- **Buffer haul priority (BLD-14).** The pump buffer's Haul is posted at `Pumps.BufferHaulPriority`, the OperatePump
  priority (40). Water is the colony's life; it should not queue behind building work.
- **Reachable stand-cell preference (CON-11).** The preferred set keeps only safe cells whose region holds a living
  agent. If none remains, all stand cells are used as before. Unit test:
  `BlockBuildScenarioTests.PreferredStand_OnlyWhereADwarfCanReach` (fails without the fix).
- **Profiler phase.** `TickPhase.BlockBuild` wraps `BlockBuildSystem.Tick`, so CON-P1 can print the Build poster's
  time. Not state; no hash or save change.
- **The script (`MonumentScript`, ViewCore).** At tick 0 it chops around the site, places a pump, digs a corridor
  into the hill, plans the tower (a `HollowBox` with the door cells cancelled and two stair flights inside) and four
  courtyard walls. The quarry room is dug at 1500 (the stone lies about 5 cells down). Two warehouses stand inside the
  quarry room (4200 and 6600), so the stone never overflows to the far hub. There is no reservoir: the river pump is
  enough. From tick 9600 `EnqueueDue` releases the plan one course (y level) at a time: the lowest course with a
  Planned entry is released once the course below has no entries left. The rule reads only plan state, so a loaded
  save carries on the same way (the save/load test checks this every 100 ticks for 2000 ticks).
- **Tried and reverted.** A CON-05 "WaitsBelow" rule (hold a cell while the one below is unbuilt) and a change to
  when `BlockBuildSystem` rechecks statuses. Neither changed the outcome once the release was course by course.
Consequences:
- The monument is complete at tick 18,316 (day 7.6), all 5 alive, no dwarf out of the Great Hall's region at any
  100-tick check, no floating block, no rejected command. Without the priority change it still completes (20,403),
  but the survival margin is thinner.
- The priority change alters the survival session: the seed-1 goldens at ticks 3000 and 6000 were regenerated.
- Dwarves stand idle between courses while the last cells of a course finish. A future task could post Build jobs
  in larger batches (for example, wait a few ticks for more Ready cells nearby) instead of relying on a script's
  release order; players releasing a whole plan will see small batches.

## ADR-067: The player's block tool places single blocks (amends ADR-065) (2026-09-27, M9-T1)
Context: at G4 the human said "building should be one square at a time" and chose single-block placement by the
player over the six shape modes. The sim's shape commands stay, because `MonumentScript`, `BlocksScript` and tests
use them.
Decision:
- **Tool.** The shape modes (Tab), the height (+/-, Ctrl + wheel), the shape buttons and the height label are gone.
  A click places one block in the cell the picked face looks into; a drag paints each new cell the cursor passes.
  Ctrl + wheel zooms the camera again.
- **Layer.** A drag stays on the first cell's layer. The cursor is the mouse ray cut with the horizontal plane of
  the first picked face (the face plane for a top or bottom face, mid-layer for a side face). The pick is the
  fallback when the ray is parallel to the plane or points away from it, and for the screenshot harness. The
  cutting plane was chosen over using the hovered pick's X/Z, because on uneven ground the hovered cell can be
  several cells away from the cell under the cursor on the painted layer. The drag always stays horizontal, even
  from a side face: the human asked for walls painted course by course.
- **Path.** Cursor cells are joined by a 4-connected grid walk, so painted neighbours share a face. This matters
  for CON-09 support (only face neighbours support) and for water (a diagonal gap leaks). Limits: 1,024 cells per
  drag, and a cursor jump of more than 64 cells is ignored (a ray grazing the plane).
- **Commands.** The release sends one `DesignateBuild(Single)` per valid cell. No new sim command was added. The
  ghost's verdicts treat the painted cells as one pending set (CON-09 counts pending cells), but separate Single
  commands are validated one at a time. So the view sends them in support order: a cell goes after a neighbour
  below or beside it that is solid, has an entry, or was sent earlier. An overhang painted from its free end is
  then accepted, as the ghost promised.
- **Tool stays active.** The tool no longer drops back to Select after a release without Shift. Painting a block at
  a time needs many clicks. Esc leaves the tool, and a right click drops the drag (M7-T1).
- **Deconstruct.** The column-box drag (ADR-065) is replaced by per-block marking. A click marks the built block
  under the mouse. A drag paints the built blocks on its first block's layer the same way. The release sends one
  `DesignateDeconstructBlocks(c, c)` per built block (CON-18), so nothing above or below the layer is marked. Marks
  are orange cubes. A click on a building is still BLD-09.
- **Tests.** The M8-T5 tests for shapes and height (`Ghost_UsesBuildShapes_InvalidCellsRedWithReason`,
  `Height_FromSliceAndKeys`) and the Deconstruct column-box assertions tested behaviour that the human removed.
  They are replaced by `View/BlockPaintTests`. The ghost mesh test now builds its ghost from shape cells directly.
- **Screenshots.** A new timed `paint` script and preset paint single blocks with the tool itself: a released
  planks L, then a planned second course on top, and a held stone drag.
Consequences:
- Goldens are unchanged, since the sim is not touched.
- A long painted course sends many commands in one tick. Each one runs a CON-09 flood over the connected entries,
  and this is fine at hand-painting sizes.
- A tall structure takes one drag per course. There is no vertical paint; a player who wants one could get it
  later from a modifier key.

## ADR-068: Build batching gathers whole courses: course check, chain batches, agent-tolerant re-check (2026-09-27, M9-T2)
Context: at G4, a whole-plan release gave trips of about 1.3 blocks, and dwarves stood idle between courses
(ADR-066). M9-T2 asks that releasing the monument plan at once averages at least 4 blocks per trip, and that the
monument still finishes by day 10 with all 5 dwarves alive. With the M8 rules, a whole release of the monument
posted 91 one-block jobs out of 103 trips, and it stalled for good at 120 of 221 blocks: sections of the tower wall
rose ahead of their neighbours, and 23 entries became `NoAccess` (no dwarf could get onto the wall top any more).
The script's course-by-course release (ADR-066) had been hiding both problems.
Decision (the simplest set that met the test; each part was measured on the whole-release monument):
- **Course check (CON-05 check 6, `BuildStatus.CourseBelow`).** An entry waits while a Build job holds a cell one
  course down within `BlockPlans.CourseRadius` = 8 horizontally (Chebyshev). A structure then rises course by
  course, as the script did, and a finished course turns `Ready` together. Only *held* cells count, so a lower
  entry that is stuck (no material, no access, given up) never holds anything up. The check comes after
  `NoSupport`, so the player still sees "nothing holds it up" first. The re-check and the running job ignore the
  job's own cells (`BuildScan.Self`), so a batch that holds two courses is not cancelled by its own lower cells.
  Radius sweep (blocks per trip / completion tick): 2 -> 3.0 / 17,500; 4 -> 4.25 / 17,000; 5 -> 4.25 / 17,500;
  6 and 8 -> 5.0 / 17,500 (measured before batch members were made strict, see below). 8 is two batch radii, and
  it covers the whole 7-wide tower.
- **Chain batches (CON-12).** Entries join within distance 4 of any batch member, not only of the seed. Members are
  scanned in order, so a batch follows a wall course. It is then stably sorted by y. Before this, a 24-cell course
  split into jobs of 9, 6, 1 and 1 cells.
- **The re-check ignores agents.** A dwarf walking on a wall top made the cells under it `Occupied`, and every
  unclaimed Build job holding one of them was cancelled and re-posted smaller the next tick (dozens of one-block
  jobs per course). The re-check now leaves agents out of the `Occupied` check (a pile or a plant still counts).
  CON-13 already waits for an agent at run time. New batch members must still be fully `Ready` (letting them
  ignore agents too gave 5.0 blocks per trip, but it put jobs on cells where a dwarf stood).
- **The script releases the whole plan** at tick 9600 (one `ReleasePlan` over the world, in `Commands`), as a
  player's "Release all" does. `NextCourse` and `CourseLeft` are gone.
- **Tried and dropped:** holding a small batch (under half a load) while a claimed Build job worked nearby. Trips
  rose to 4.5 blocks at that point in the work, but completion slipped from 17,000 to 20,000 ticks and idle time
  doubled.
Consequences:
- Monument, whole release: 46 trips, 4.80 blocks per trip. It completes at tick 17,200 (day 7.2); ADR-066 had
  18,316 with course-by-course releases. All 5 dwarves are alive, and no dwarf is walled in and no block floats at
  any 100-tick check. Jobs per course are now usually 10, 10, 3 and 1; the singles left are stair steps (each step
  is its own course) and the door lintel.
- Dwarves still wait while the last batch of a course finishes (about 10,700 idle agent-ticks between ticks 9,600
  and 17,400, out of 39,000). Overlapping courses would need batches that span a course and the one above it.
- A structure within 8 cells of another one waits for the other one's held lower course. This is short, because
  only held cells count.
- `OnlyReadyEntriesPostJobs_StatusesExplainTheRest`: the 2-cell ledge at y + 1 now waits (`CourseBelow`) for the
  held column 5 cells away, instead of getting a job. The assertion was updated to the new rule.
- Goldens are unchanged (the survival session has no plan entries).

## ADR-069: The top bar wraps beside the toolbar; invalid cells are outlined red and drawn through blocks (2026-09-27, M9-T3)
Context: G4 follow-up. In the monument shot at tick 14000 the top bar's first row (with the "Pump has no water"
alert) and its plan line made the bar wider than the space right of the toolbar, and "Day 6" was drawn over
"Cancel (Z)". The block tool's red cells were faint: a red cell sits in the solid block that makes it invalid, and
the ghost was only 0.02 larger than that block, so the m9t1 blocks shot hid it almost fully. Stuck plan ghosts
(VIEW-22) were a pink tint on light stone. The M9-T1 paint shot had the mouse label over the painted L.
Decision:
- **Wrap, then drop.** `HudLayout.ArrangeTopBar` (ViewCore) wraps the top bar's items (first row by item; the plan
  line by `PlanText.Atoms`, each item with its header and the separator before it, which is dropped at a line
  start) to the width between the toolbar (+ 12 px) and the right margin. The bar stays right-aligned at the top.
  Only if one item is wider than that space does the bar move below the toolbar and wrap to the screen width. The
  first row and the plan line each start their own line, as before. Wrapping was chosen over moving the bar,
  because the bar then stays where the player looks for it; the colonist panel keeps the left side.
- **Invalid cell style** (`InvalidCellStyle`): the unreachable red at alpha 0.8 plus 12 opaque dark red edge bars
  0.1 cells thick. The tool ghost's invalid cells are their own mesh, 0.06 larger than the cell, drawn without a
  depth test (`TranslucentMesh.XRay`), so a red cell inside or behind a block shows. Stuck Released plan ghosts use
  the same fill and outline, inset like the other plan ghosts, with the depth test (there can be many of them).
- **The mouse label avoids the tool ghost.** With the block tool, the ghost's projected box joins the billboards
  that `LabelLayout.PlaceTooltip` keeps clear of (`ScreenRect.Bounding`, `BlockGhost.Bounds`).
Consequences:
- `BlockGhostMesher.Build` now holds only the valid cells, and `BuildInvalid` the red ones. The test
  `ToolGhostMesh_PaletteColourOrRed` was updated to count the red cell in the second mesh (the behaviour changed on
  purpose). `BadAlpha` and `PlanGhostMesher.StuckTint` are gone.
- A red tool cell shows through hills and walls in front of it. That is wanted for the cursor's own ghost, where
  the player is looking.
- The mouse label can sit further from the cursor during a long drag (below or beside the whole drag).

## ADR-070: Three more construction blocks from the existing items (2026-09-27, M9-T4)
Context: G4 asked for more building materials. The backlog suggested rubble, a dirt or clay block and a wood beam,
and said to add items only if a block needs them. Dirt has no drop item (`blocks.json` Dirt `drop: null`), so a
dirt or clay block would need a new item, a new drop and a hauling path for it.
Decision:
- **Blocks 11..13** in `data/blocks.json`, the `BlockId` enum and `palette.json` (CON-01):
  - `Rubble` "Rough stone": 1 stone, builds in 10 ticks, digs in 30. The cheap, fast stone block.
  - `Beam` "Wood beam": 2 logs, 25 build ticks, 30 dig ticks. A dark red timber.
  - `Slate` "Slate tiles": 3 stone, 50 build ticks, 70 dig ticks. The expensive blue-grey stone.
  No new items: each material trades cost against build and dig time. A clay block waits for a clay item.
- **Colours** differ from every other block colour (a test checks it). Rubble was first `#6f665a` and read as the
  same grey as Masonry under daylight in the `materials` shot; it is now the darker `#5a4e40`.
- **Screenshot script `materials`** (`MaterialsScript`): a 2x2 released sample of every construction block in
  Blocks-menu order with a planned course on top. It digs the `digchop` pit 5 layers deeper, because grass and 4
  dirt layers cover the stone near the hub (a 2-deep pit gives about 6 stone). All six are built by tick 12000.
- **Test data:** `BlockContentTests`' "json id with no BlockId value" case added id 11 after the last block. Id 11
  is now Rubble, so the case adds id 99 after Slate instead. The case still checks the same rule.

## ADR-071: A released entry the plan will support waits for support instead of reading NoSupport (2026-09-27, M10-T1)
Context: G5 issue 5. After the monument's whole-plan release, the inner stair steps (which lean sideways on the
tower wall, with air below them) failed CON-05 check 5 (placement support) until the wall course beside them was
built. They read `NoSupport` and drew red with an outline (VIEW-22), though they built fine later: 6 at tick 11,000
and 2 at tick 14,000. Red is meant for entries the builders can never do.
Decision:
- **New status `WaitSupport`**, checked in the slot of check 5: when placement support fails, the entry is
  `WaitSupport` if CON-09 *plan* support holds for it (a down/sideways path through entries of either state reaches a
  solid block), and `NoSupport` otherwise. So `NoSupport` now means "nothing in the plan or the world can hold it up",
  for example after a cancel removed the entries it leaned on.
- The enum value sits before `NoSupport` (`BuildStatus` is derived, never saved or hashed, so renumbering is safe).
- `BuildScan.PlanSupported` runs one `Support.PlanSupported` flood over every entry, on the first entry per scan
  that fails placement, and caches it for the scan. It is O(entries). Over `Support.PlanFloodCap` (16,384) collected
  cells nothing counts as supported, so a huge plan falls back to the old `NoSupport` (conservative).
- An entry that leans on a *Planned* (not released) entry also waits: the player can release the rest.
- The view: `WaitSupport` is not stuck, so it draws as a normal released ghost. Its hover text is "waiting for
  support".
- Neither status posts a job, so the build order, the golden hashes and the monument timeline are unchanged.
Consequences:
- `OnlyReadyEntriesPostJobs_StatusesExplainTheRest`: the ledge cell beside a released entry on the post now reads
  `WaitSupport` (was `NoSupport`). The behaviour changed on purpose. The truly floating case is covered by the new
  `LeaningOnPlannedCells_Waits_TrulyFloating_StaysRed`.

## ADR-072: The block tool's ghost shows cells beyond the free stock in amber (2026-09-27, M10-T2)
Context: G5 issue 11. A Stone-wall drag with 0 stone stored looked valid; the shortfall only showed after the release,
in the top bar (orange) and as `NoMaterial`. The task asks for the drag's cost against free stock (stock minus what
released blocks will use), with cells beyond it amber.
Decision:
- **Free stock** (`FreeStock`, ViewCore): per item, `Storage` totals minus `Needed(Released)`, clamped at 0. For a
  plan-mode drag it is also minus `Needed(Planned)`: planned blocks do not use stock yet, but VIEW-23 already calls
  the Planned part short when the released plus planned need is over stock, so a plan drag is compared with what the
  whole plan would leave. The tooltip says "free after the plan" then.
- **Which cells are short:** the valid cells in paint order take the free units (cost per block each); every valid
  cell after the stock runs out is short. Paint order is what the player sees growing, so the amber part is the tail
  of the drag. Red (invalid) cells take no stock.
- **Short is not invalid:** short cells are still sent on release. They wait as `NoMaterial` until stone comes in,
  as before. They draw in amber (`designations.short`, `#ffd21f`) with a darker outline (`ShortCellStyle`), in the
  ghost's normal depth-tested mesh, not the no-depth-test red mesh (ADR-069): amber is advice, not an error. Amber is
  kept at least 0.3 (RGB distance) from red, the deconstruct orange and every palette block colour (tested).
- **Tooltip:** a line after the count and cost: "40 stone free", or "Only 3 stone free: 2 blocks short (amber)", or
  "No stone free: …".
- **Cadence (CON-06):** `FreeStock.Of` is O(entries + buildings). GameRoot takes it with the top bar's plan line (every
  10 frames) while the block tool is active, and the ghost cache also keys on the stock snapshot. Ghosts made without
  stock (scripts, `Release`) have no shortage.
Consequences: a released drag's ghost can show amber for up to 10 frames after the release changed the free stock;
harmless. The paint screenshot's held stone drag (0 stone stored) is now amber, with red where it crosses the L.

## ADR-073: A block-tool drag from a side face paints that face's vertical plane (amends ADR-067) (2026-09-27, M10-T3)
Context: G5 Q2 (the recommendation was taken): a wall face should be paintable in one drag. ADR-067 kept every drag
horizontal, so a 7x7 tower of 8 courses took 8 drags per face.
Decision:
- **Which plane.** A block-tool drag that starts on a top or bottom face stays on the first cell's layer, as before.
  One that starts on a side face stays in the vertical plane of its first cell, parallel to the picked face: X is
  fixed for an east or west face, Z for a north or south face. The first cell is still the cell the face looks into,
  so the wall face grows beside the picked block (that block supports the first cell).
- **Cursor.** The mouse ray is cut with the picked face's own plane (the block's surface), so the painted cell is the
  one under the cursor on that face. The pick stays the fallback (its cell projected onto the plane) when the ray is
  parallel or points away. The path between cursor cells is the same 4-connected walk, in the plane (ties step
  along Y). "A rectangle" means the cells the cursor sweeps over, not a box between two corners: painting stays
  painting (ADR-067).
- **Send order.** The valid cells are sorted bottom-up (a stable sort, so a horizontal drag keeps its paint order),
  then `SupportOrder`. A wall painted from the top down is sent from the bottom, and each command is applied with
  support. No new sim command.
- **Shortage (ADR-072 amended).** The free stock is taken in the same bottom-up order, so on a wall face the top cells
  are amber: the ones sent last, which wait as `NoMaterial` last. On a horizontal drag nothing changes (the tail of
  the drag is amber).
- **Deconstruct stays horizontal.** The task names the block tool. Deconstruct by block still paints the first
  block's layer; `PaintDrag` takes the vertical mode as an option, so it can follow later if the human wants it.
- **Reach.** Dwarves reach one level up, so the upper cells of a tall painted wall wait (`BelowFirst`,
  `WaitSupport`) until there is a stand cell. That is the same as a course-by-course wall.
- **Screenshots.** A new `wall` script and preset: the `paint` drags, with the harness holding a vertical Wood planks
  drag (4 wide, 6 high) beside the L. It needs 24 logs with 20 free, so its top row is amber.
Consequences: the controls text and the HUD hint name both drags. Goldens are unchanged (no sim change).

## ADR-074: The course check is local, and CON-12 holds small batches while the course below is held nearby (amends ADR-068) (2026-09-27, M10-T4)
Context: G5 issue 2. After a whole-plan release of the monument, about 27% of dwarf time was idle (measured here as
29.1%: 11,051 of 38,000 living agent-ticks with `AgentState.Idle` from the release at tick 9,600 to completion at
17,200). ADR-068's course check (radius 8) held a whole course back until every cell of the course below was placed,
so only one course was ever in jobs: 10, 10, 3 and 1 blocks for 5 dwarves. M10-T4 asks for at most 15% idle while
keeping at least 4 blocks per trip and the monument's checks (done by day 10, no dwarf walled in, no floating block,
all 5 alive).
Decision:
- **CON-05 check 6 is local:** `BlockPlans.CourseRadius` 8 -> 1. An entry waits only while a cell under it or
  diagonally under it is held by a Build job. So a cell whose supports are built may start while other parts of the
  course below are still in jobs (the task's wording). Radius 0 (no check) put a stair step into a red status mid-build,
  so the local check stays.
- **CON-12 small-batch hold:** a batch under 60% of a full load (`FullEnough`: 6 of 10 for Masonry) is not posted
  while a Build job holds a cell one course below its seed within `BlockBuildSystem.HoldRadius` = 8 (ADR-068's old
  radius). Those cells turn the course above `Ready` as they are placed, so the trip waits to fill. A small batch with
  nothing held below nearby (the last course, a lone hand-placed block) goes out at once. Its cells read `Ready`
  meanwhile (a batch property, not a per-cell one), so no new status.
- **Measured** on the monument (seed 1, whole release). Local radius / hold threshold -> idle share, blocks per trip,
  completion tick:
  - ADR-068 (8, none): 29.1%, 4.80, 17,200.
  - Radius only, no hold: 1 -> 37.6%, 1.92, never done; 2 -> 24.6%, 2.10, never done; 3 -> 6.1%, 2.83, 18,000;
    4 -> 13.5%, 3.20, 17,800. Small trips everywhere: an idle dwarf takes each newly Ready cell alone.
  - Radius 1 with the hold at a batch of 4 / 5 / **6** / 7 / 8 -> 7.6% / 7.9% / **11.1%** / 20.8% / 24.3% idle,
    4.09 / 4.09 / **4.25** / 5.39 / 5.02 blocks per trip, 15,900 / 16,300 / **16,200** / 15,500 / 16,500.
  - Radius 2 with the hold at 5 or 6: about 23% idle.
  - 6 was taken: the lowest idle share with a margin on both limits (11.1% against 15%, 4.25 against 4.0).
Consequences:
- Monument, whole release: 52 trips, 4.25 blocks per trip, complete at tick 16,200 (day 6.75; 17,200 before), idle
  11.1% (3,649 of 33,000 agent-ticks). All 5 alive, no dwarf walled in, no floating block, no stair step red at 11,000
  or 14,000.
- Two courses of a structure are often in jobs at once, and a batch can span them (CON-12 `Join` already allowed it).
- `OnlyReadyEntriesPostJobs_StatusesExplainTheRest`: the ledge 5 cells from the held column now reads `Ready` (it read
  `CourseBelow` under ADR-068) and is not posted while that column's lower block is held (a 1-block batch). The
  assertion was updated to the new rule. `UpperCourse_WaitsForHeldCourseBelow` was replaced by
  `UpperCell_WaitsForHeldCellsBesideBelow` (the local check) and
  `UpperCourse_FullEnoughBatchStarts_SmallOneWaitsForHeldCourseBelow` (the hold; fails under ADR-068).
- The remaining idle time is mostly the last course and the door lintel (few cells left), and short waits while a
  held-back batch fills.

## ADR-075: The materials script gets a pump and no levee (2026-09-27, M10-T5)
Context: G5 issue 8. The `materials` screenshot script had no pump; stored water ran out in the day-5 drought and
the colony was lost at tick 15,295. The backlog allowed a levee reservoir as in SurvivalScript (ADR-055/056).
Decision:
- `MaterialsScript.Commands` adds a Water Pump at the nearest wet site, found the way the `build` script finds it
  (`ScreenshotScripts.FindSite(..., wet: true)`), so the script stays computed from the world (ADR-020). On seed 1
  it is (54,18,76) rotation 0, the monument's pump, on the river bank; its worker uses the stand cell one level up.
- No levee or reservoir. The river bank intake dries in the drought (4,704 dry drought ticks), but the pump fills
  the store to about 110 water before it, and the store is 90 or more at every day report until the river returns. A reservoir
  would add digging that competes with the samples and the pit for no gain in this run.
- The pump's 12 logs come from the starting stock; the Planks and Beam samples still have enough (6 logs left).
Consequences: `--script materials --ticks 24000` ends with 5 of 5 alive. A longer drought or more dwarves would
need the SurvivalScript reservoir. `MaterialsScenarioTests` checks the samples at 12,000 and 5 alive at day 10.

## ADR-076: Levees have no entrance (2026-09-27, M11-T1)
Context: G6: "Not sure why levees have doors, what are they for?" Every building had an `entrance` from the shared
template. The levee has no worker and no storage, so its entrance only told the builder where to stand, and it
reserved a cell that no footprint, block or plan could use. The ghost drew it as a door tile.
Decision:
- `entrance` may be `null` in data (`BuildingDef.HasEntrance`). ContentDb allows it only for a `ground` building
  with no workers, no storage, no producer and not prebuilt-only. The levee is the only one.
- Stand cells of such a building are the standable cells in reach of its footprint (ARCH-07, the 26-neighbourhood:
  beside it, and one level up or down, diagonals included) outside every building, by ascending cell index
  (`BuildingSystem.ReachStandCells`). Builders already walked to any reach cell (JobGoals `Building` mode), so the
  job goals are unchanged.
- Placement (BLD-02): instead of the entrance check, at least one stand cell must exist now, else the new
  `PlacementResult.NoStandCell` ("No room for a builder beside it"; added at the end of the enum). A stacked levee
  finds its stand cells one level down beside the levee below, so the old ADR-040 fallback is covered. A third
  levee on a blueprinted stack on open ground still has none.
- Nothing around a levee is reserved: `IsAnyEntrance` (Overlaps), CON-08 and CON-13 skip buildings with no
  entrance. A later footprint may take a levee's last stand cell; its jobs then wait until one frees up. Accepted:
  the same happens to a dig or a block, and the player sees the site not progress.
- Jobs of a building with no entrance are posted at its origin (`Building.JobCell`), which is also where Deliver
  sources measure distance from. `StandCell` (agents pushed out on BLD-07, refund piles on BLD-09) is the first
  stand cell, else the cell above the top (piles then spiral out, ECO-08).
- The ghost's `Entrance` is null for a levee, and the Godot layer draws no entrance tile.
- The `build` screenshot script steps its levee line along +x (it used the entrance direction). The screenshot
  harness gained `--ghost <building id>` (`GHOST=levee`) to show a green ghost of that building.
- Old tests that asserted the levee's entrance (`LeveeInRiver_LowersDownstream`, `Levee_StacksOnLevee`, the pump
  stand-cell test's levee line, and Deliver/Construct job targets) now assert the new rule: `JobCell`,
  `NoStandCell`, and a levee on the bank edge being placeable.
Consequences: the survival session's levees (including the reservoir seal at (67,17,71)) build as before; its hash
`e1ddb97096eb0267` and every golden are unchanged, because builders already stood on any reach cell. A levee can now
be built into a notch reached from one side at any rotation.

## ADR-077: The colony starts with a wagon of building supplies; starting stock moves into data (2026-09-27, M11-T2)
Context: G6: "can they arrive with a wagon full of building resources for us to start with?" The Great Hall's
starting stock (40 berries, 30 water, 30 logs) was a C# array in `WorldFactory` (ADR-043), and every stone had to be
quarried first.
Decision:
- New `wagon` building in `data/buildings.json`: 2×2×3, prebuilt-only, a storage that accepts log and stone with
  `"receives": false`, `startStock` 40 log and 60 stone, `removableWhenEmpty`, cost 20 log (only the teardown refund
  uses it: 10 logs). It keeps an entrance because it is a storage (ContentDb, ADR-076).
- `startStock` is a building field (BLD-15). The hall keeps 40 berries and 30 water; its 30 logs moved to the wagon,
  which holds 40. ContentDb checks each start stock against the building's storage (accepted items, caps).
- World creation places each non-hub start building, in data order, complete, at the nearest site beside the hall:
  footprint gap 2..4 in x/z, height within 2 of the hall's floor, sorted by (gap, rise, z, x, rotation). The site
  must pass BLD-02 bar the prebuilt-only check, be dry, and have its entrance reachable from the hall's entrance both
  before and after it is placed (else it is taken back and the next site is tried). No site throws. On seed 1 it is
  at (36,24,55) rotation 0, entrance (36,24,54), 2 cells west of the hall.
- `receives: false` (BLD-16) is enforced in the two room checks (`JobBoard.StorageRoom`, `WorldActions.FreeCapacity`),
  so hauls, deliveries and pump output never pick it; it is a source like any storage, and the HUD totals include it.
- `removableWhenEmpty` (BLD-17): Deconstruct is rejected with `NotEmpty` while it holds anything; the tool says
  "The Wagon still holds N items: use them first". Empty, it comes down with the usual half-cost refund.
- The view draws the wagon in its own palette colour with four wheels and a cream cover (styling keyed by the
  `wagon` id in `BuildingVisuals.WheeledIds`); no sim data.
- Scripts that depended on the old start (tests kept, intents unchanged): the extra 70 items to spend and store change
  who does what when, so the survival tunnel and the monument quarry room are dug later on seed 1. `SurvivalScript
  .BreachTick` 7200 → 7500 (repair still +300: at 7200 the breach cells were not dug by the repair tick, so a
  repair levee's site was blocked; a longer repair delay instead caught no dwarf in the flood), `MonumentScript.YardTick` 6600 → 7200 (the far warehouse site was not dug yet). The
  `build` screenshot script now keeps its sites clear of every standing building (the first levee line walled in the
  wagon's entrance and a builder dropped 10 logs), and since the wagon's logs finish its sites by about tick 400, its
  mid-build tick is 200 (was 500).
Consequences: goldens regenerated (the start world holds a new building and the stock moved). A colony can build a
warehouse, the pump and some levees before the first chop, and has 60 stone for masonry from day 1. Starting stock is
now changed in data only.

## ADR-078: Stair-down dig mode and dig wait reasons (2026-09-27, M11-T8)
Context: G6: "How do I dig down deeper than 1 tile?" A box dig deeper than one level leaves its lower cells waiting
forever (DSG-09: a dwarf climbs only 1 level), with no word of why, and the slice keys were not shown anywhere. The
backlog asks for a stair-down mode, hover reasons, and a slice hint, with the logic in ViewCore.
Decision:
- Stair geometry: each step is a column of 3 cells (the step's floor cell and two above it), because PTH-01/05 need
  a stand cell plus headroom and a step down needs b+2 clear. Step k is one level down and one cell along the drag's
  main XZ axis (X on a tie; +X when the drag does not move, so a click still digs somewhere defined). The bottom is
  the second pick's cell clamped to the view level and never above the first cell, so "to the view level" works both
  by lowering the slice and by dragging onto lower ground; with neither, the tool says to lower the view level.
- The view sends one `DesignateDig` per column. No new sim command, codec change or sim rule: the existing
  exposure, top-down and strand rules already dig a staircase in order, and replays of old logs are unchanged.
- `DigStatus` (DSG-10) is a pure sim query (the view may not reimplement sim rules). A `DigUnreachable` mark that
  `JobGoals.DigBlockedByStrand` still holds reports `WouldTrap`, not `Unreachable`: that is the case the player can
  fix with a stair, and the message says so.
- Hotkey T toggles the mode only while the dig tool is active (T is otherwise free); the mode is kept across tool
  changes so a player digging several stairs does not re-pick it.
- The `stairs` screenshot site is anchored on the hall only (not the terrain it digs), so the camera does not move
  between tick counts; it heads north on flat ground because the seed-1 ground east of the hall slopes away and the
  slice then hid the stair. The scenario test keeps the east site down the slope (a harder case for the strand rule).
Consequences: no goldens change. Digging deep is a two-step habit (lower the view level, drag a stair) that the HUD
now spells out. A stair is a covered tunnel after the first steps, so it is seen through the slice, not from above.

## ADR-079: Gravity for piles and buildings: instant falls, whole-building collapse, a new tick slot (2026-09-27, M11-T9)
Context: G6 follow-up: "buildings, items, log piles with no ground beneath them should fall down to ground." Piles
could float (a dig under a pile, a dig drop or CON-17 refund over a cave), and no building could lose its ground
because DSG-02 never marked, and `Dig` refused, any cell under a building. Spec: `docs/specs/gravity.md`.
Decision:
- **Instant falls.** A pile that does not rest moves straight to its rest cell in the same tick (GRV-03), rather than
  one cell per tick. No in-flight state, so no save format bump and no new hashed state; the view sees a normal
  `ItemPileChanged` pair. Landing merges per ECO-08, else `PlacePile` from the rest cell.
- **Tick slot.** `Gravity.Tick` is ARCH-01 step 10b, after the agents (where every dig and teardown happens) and
  before `WaterGrid.EndTick` / `PathGrid.SyncWorldChanges`, because a collapse writes Air over a footprint and nothing
  may call `SetBlock` after that point. Buildings first (to a fixed point, so stacks cascade), then piles, so the
  refunds of a collapse fall in the same tick.
- **The mutations are WorldActions.** `WorldActions.FallPile` and `WorldActions.Collapse` (internal, like `PlacePile`
  and `TearDown`) do the writes; `Gravity` only finds what must fall (hard rule: one action API).
- **Undermining is allowed** for complete, not anchored buildings only (GRV-06): DSG-02 marks their floor and
  `Dig` digs it. Floors of blueprints, sites and buildings being deconstructed stay protected (a site's jobs, the
  BLD-07 push-out and the teardown all assume their ground). This is the only rule change for digs.
- **Anchored buildings** (`prebuiltOnly` and not `removableWhenEmpty`: the Great Hall) never collapse and their
  floor is never dug. The whole colony depends on the hall (spawn, food, water, the strand rule's region), and losing
  it to one stray dig drag would be a trap. The wagon is not anchored: it collapses like a warehouse.
- **Partial support holds** (GRV-01): a building stands while any bottom cell has solid ground (or another building)
  under it. No overhang physics.
- **Collapse, not a fall.** A building does not fall intact; it is removed and drops half its cost (the BLD-09 rate,
  so a collapse is no cheaper than a teardown) plus all stock, as falling piles from its origin. Jobs naming it are
  cancelled (not failed). Agents on it or in it are moved after its footprint turned to air: to its stand cell if they
  can stand there, else the first standable cell of the ECO-08 spiral around its rest cell (a walled-in levee's stand
  cell is the cell on top of it, which is air after the collapse).
- **Levee stacks** collapse whole (GRV-08): when the bottom levee goes, the one on it no longer stands and collapses in
  the next pass of the same step. A blueprint or site that loses its support is cancelled (BLD-09 refunds).
- **CON-10 extended** (GRV-09): `Support.Depends(sim, cell)` adds the footprints of the buildings a removal would
  collapse, so a built block resting on a building keeps that building's last floor cell from being dug. It also
  returns true for the floor of a building that holds its floor (GRV-05/06: the hall, a blueprint, a site, a building
  being deconstructed). So a Dig job posted before a Deconstruct order waits (stand-down, no JOB-08 failure) like any
  CON-10 dig. All dig callers (poster, job selection, work start, the dig step, `Dig`, `DigStatus`) get this through
  the one function.
- `DigStatus` reports `Support` for a protected building floor; the hover text now reads "something built rests on
  it" (it covers built blocks and buildings).
Consequences: piles never float; a player can dig out a building (except the hall) and it collapses into its pit. Dig
drags that cover the ground under complete buildings now mark it (they used to skip it). The seed-1 golden changes at
tick 6000 (ticks 0, 1200 and 3000 unchanged): in the survival script's two-level quarry (x 84..85, y 18, z 56..60)
stone drops left on the upper layer now fall when the cell under them is dug (ticks 3904..4627). No building
collapses in that run. The headless survival hash changes for the same reason (5/5 alive; one Flee need job fails at
tick 8045 on the changed timeline, well after the last fall; need jobs are re-posted, ADR-031).

## ADR-080: Fine block shapes are a form on a solid 1 m cell; cost scales by an integer percent (2026-09-27, M11-T10)
Context: G6 follow-up: "Can we have a .25 meter pixel?" The human chose fine shapes on the 1 m grid (slab, stair,
pillar) drawn at 0.25 m detail, not a 0.25 m sim grid (about 64 times the cells). The backlog asks for shapes in data,
the shape and rotation in the build command, save and hash, and an ADR on how the sim treats them. Spec: CON-19..22.
Decision:
- **Every shape is one solid cell** for paths, water, support (CON-09/10), gravity (GRV-01) and buildings (BLD-02).
  Solidity stays a property of the block byte, so none of the hot paths (water CA, path flags, regions, trials)
  change or slow down. A Stair block is not a half-step for walking: dwarves already climb one level per step, so a
  stair of Stair blocks is walked like a stair of full blocks. A slab is a full floor at the cell's top for the sim
  and looks half-high; that mismatch is accepted for the POC (walking on the drawn slab top is a view concern, M11-T11).
  The alternative (half-height floors, water over slabs, piles resting at half height) touches PTH, WAT and GRV and
  was not asked for.
- **Form = (shape, rotation)**, `BlockForm`, packed into one byte (`shape * 4 + rotation`, 0 = Full). Shapes and
  their rotation counts are data (a `shapes` list in `blocks.json`, not a new file, so `ContentDb.Load` keeps its
  signature). The enum is kept in step with the data like `BlockId`.
- **Storage:** a sparse `SortedDictionary<cell index, packed>` in `VoxelWorld` for cells that are not Full, instead
  of a second dense byte array. Only built blocks with a fine shape have one. Any `SetBlock` removes the cell's form,
  so a dig, a collapse or a new block cannot leave a stale form, and the invariant "a form only on a built block" holds
  through one write path. It is hashed only when not empty, so worlds without shapes hash as before and no golden
  moves.
- **Cost varies by shape**: `max(1, ceil(cost x costPercent / 100))` with Slab 50, Stair 75, Pillar 25. This is an
  integer and never 0, so the 1-cost blocks cost 1 in every shape, and Slate and the 2-cost blocks get cheaper
  slabs and pillars. The dig refund is the shaped cost, so the refund stays 100% (CON-17). Build ticks and hardness do
  not change with the shape (the simplest choice).
- **Batches** join only entries of the same block and shape (any rotation), so a job has one cost per cell and its
  pickup is `cells x cost`. Each Place step carries its own form in `JobStep.Target` (block in the low byte, packed
  form in the next). A Full step's target equals the old block id, so existing job hashes do not change.
- **Command:** `DesignateBuild` gets an optional `Form` (default Full), so all existing callers and scripts keep
  compiling unchanged. The codec writes the shape and rotation as two raw bytes, not the packed byte: a command with a
  bad rotation must replay as rejected, and packing would mask it into a valid one. The command log lives only in the
  save, which is version-gated (SAV-04), so the codec change comes with the bump to save v7 and needs no second
  version field. New rejections `BadShape` and `BadRotation` come after `NotBuildable`. A repaint that changes only
  the form cancels the Build job that holds the cell, as a block change does.
- **Save v7:** a `BlockForms` section right after `Blocks`, a form byte per plan entry, and the command's form.
Consequences: no golden or headless hash changes (no script paints shapes, and a Full form adds nothing to the hash).
ViewCore still draws every built block full and prices the paint ghost and shortage with the Full cost; M11-T11 adds
the mesher patterns, the shape picker and rotation, and switches those to `CostOf(block, shape)`.

## ADR-081: Fine shapes are drawn from 4x4x4 sub-cell patterns; shaped cells leave the greedy pass (2026-09-27, M11-T11)
Context: M11-T11 draws the CON-19 shapes at 0.25 m detail and adds them to the block tool (VIEW-27).
Decision:
- **Patterns, not meshes.** Each form is a 64-bit mask of 4x4x4 sub-cells (`ShapePattern`), built from the CON-19
  geometry. The same emitter (`ShapeMesher`) draws the terrain, the tool ghost and the plan ghosts, and a new shape is
  one more pattern.
- **Mesher split.** A shaped cell is cleared from the padded grid, so the full cells' greedy pass draws every face
  toward it, as toward air. The shaped cell then culls its own sub-faces against the neighbours' patterns (a full
  solid neighbour covers its whole side). A full face that the shape partly covers is still drawn whole, behind the
  shape. This costs a few hidden pixels, gives no coplanar pairs (the shape never draws the face against the full
  cell), and keeps the greedy pass unchanged. Shaped faces merge only within one cell's 4x4 layer, not across cells.
- **Picking** steps 0.1 cell behind the hit instead of 0.5, so inner faces (slab tops at 0.5, stair risers) resolve
  to the shaped cell. 0.1 is below the 0.25 m sub-cell and far above float error at world coordinates.
- **Tool.** V cycles shapes (P and T were taken; Tab was avoided on purpose in M9-T1) and R turns a stair. R already
  rotates the Build tool; each tool reads it only while active. The shape stays chosen between releases and block
  changes. A one-rotation shape resets the rotation to 0, so the sim never sees `BadRotation`.
- **Ghost styles.** Only valid cells show the shape. Red (invalid) and amber (short) cells keep their whole-cube
  styles, so the warning stays as visible as before.
Consequences: MESH-P1 has a second case with every top cell of the busiest seed-1 chunk shaped (1,024 cells, about
5,800 quads, median about 3.8 ms Release against 6 ms). The sim is unchanged: every shape is still a full solid cell
(CON-21), so a dwarf stands on the cell's top, 0.5 above a drawn slab. No golden changes.

## ADR-082: Economy and crafting: inline recipes, carried inputs, make/keep orders, a trade wagon at the hall (2026-09-27, M11-T3)
Context: at G6 the human picked **economy and crafting** as the missing gameplay element: workshops that refine
materials (sawmill, stonecutter) and trade wagons. M11-T3 writes the spec (`docs/specs/crafting.md`, CRF-01..24,
CRF-P1). M11-T4..T7 build it, and the scope must fit those four tasks.
Decision:
- **Data placement.** Recipes live in a `workshop` block on their building, and offers and the schedule in a `trader`
  block on the `trader` building, both in `buildings.json`. `ContentDb.Load` keeps its four-file signature, as the
  shapes did (ADR-080). The two new items are appended after `water`, so existing item ids and hashes do not move.
- **Refined blocks.** Wood planks (1 planks), Polished stone (2 cut stone) and Slate (3 cut stone) move to refined items,
  as the backlog asked. Masonry, Rough stone and Wood beam keep raw costs; a beam is a hewn log. The counts do not
  change, so CON-20 shaped costs, batches and view totals keep working unchanged. A Sawmill makes 2 planks per log, a
  reason to build it. The Stonecutter is 1:1, so refined stone costs labour, not extra stone.
- **Starting refined stock.** The starting wagon gains 20 planks and 20 cut stone. Without it, every refined block
  would need a workshop first, and each screenshot script that paints Wood planks, Polished stone or Slate (paint,
  wall, blocks, shapes, materials) would have to build workshops and wait for orders. The stock is small enough that
  any real build still needs a workshop or a trade.
- **Workshops have no input stock.** The crafter fetches one batch of input from storage, carries it to the entrance
  and works its cycles there. Each `Craft` step turns carried input into output in the workshop's `Stored` (the output
  buffer, like a pump's). This is one new step and no new container. A preempted crafter drops the input as a pile,
  and it is hauled back by the existing rules. One recipe has one input and one output item, and a workshop has 1
  worker and at most one Craft job.
- **Orders: both modes, one per recipe.** "Make N" counts outputs made and removes itself when done. "Keep N" compares
  the colony stock (complete storages, workshop outputs and carried items; piles excluded) with N each time a job is
  planned. Orders run in recipe order, not insertion order, so there is no reordering UI and no list to save beyond the
  entries. Two workshops keeping the same item may each start one batch past N. That overshoot is bounded (one batch
  per workshop) and accepted.
- **Job kinds.**
  - `Craft` (30) and `Unload` (30) sit above Dig, Chop and Build (25), so workshops run in a busy colony, and below
    the pump (40), Harvest (35) and construction (45, 50).
  - `Trade` (35) covers loading payment and unloading goods. It is above crafting because the wagon leaves on time.
  - `Unload` is its own kind, so `Pumps.IsBufferHaul`, which identifies a pump haul by its step shape, is not fooled.
  - Craft and Unload are not JOB-12 sources; the region filter and the workshop status cover the unreachable case.
- **The trade wagon arrives at the Great Hall**, not at the map edge.
  - An edge arrival needs a long path that may cross the river or the drought bed, and a travel state. The hall is
    where the storage is.
  - The trader is placed complete by the BLD-15 start-site search, the same code that placed the starting wagon. With
    no site, the visit is skipped with an event.
  - It is anchored like the hall (no collapse, no undermining, no deconstruct), so a visit cannot end mid-deal by
    gravity.
- **Schedule** is a pure function of the tick (first arrival 3000, every 7200, stay 1800), like the weather. Only the
  current visit is state.
- **Pay first, then goods.**
  - A deal is `(offer, lots, paid, granted)`. Dwarves carry the give item from storage onto the wagon (`Pay` step).
    Each fully paid lot adds its goods to the wagon's storage at once.
  - That storage is `receives: false`: a source like the starting wagon (BLD-16), counted in the totals, and emptied
    by `Trade` unload jobs.
  - At departure, the colony keeps what it paid for: unloaded goods and partial payments are dropped as piles at the
    entrance. Granted payments leave with the wagon.
  - `AcceptOffer` checks the free stock, minus what earlier deals still owe, so a deal can always be paid unless the
    player spends the stock meanwhile.
- **Save** bumps twice, v8 in M11-T4 (orders, new jobs) and v9 in M11-T5 (the trader section). Each task owns its
  format change.
- **Hash.** Orders are hashed only for workshops, and the trader section only while a trader is here.
Consequences:
- M11-T4 regenerates the goldens: the wagon's start stock and the block costs are in the start state.
- Tests that build Wood planks, Polished stone or Slate need the refined items. Unit and scenario tests may stock
  storage directly, and scripts use the wagon's stock or build a workshop.
- M11-T5 changes the seed-1 hashes from tick 3000 on, because the trader writes BuildingSolid beside the hall.
- Out of scope: money, prices, multi-input recipes, worker assignment, skills, quality, factions, a travelling trader,
  selling anything outside the offers, and more than one trader definition.

## ADR-083: Workshop jobs: GoToBuilding for unloads, cancel on teardown, a stand-cell craft check (2026-09-27, M11-T4)
Context: M11-T4 builds CRF-01..14. A few points in the spec were loose or clashed with existing mechanics.
Decision:
- **Unload goes by GoToBuilding.** CRF-12 wrote `GoTo(entrance)` as the first Unload step. The job uses
  `GoToBuilding(workshop)`, as the pump buffer haul does (BLD-14), so the dwarf stands where `PickUpFromStorage` can
  reach the workshop (its stand cell, which may be one below or above a blocked entrance). crafting.md is updated.
- **Teardown cancels, it does not fail.** CRF-10 said a claimed Craft job on a workshop that is no longer complete
  fails at its next Craft step; CRF-14 said the workshop's jobs are cancelled. `Workshops.Tick` cancels every Craft
  job of a workshop that is not complete (claimed ones too: the crafter drops its input, which is hauled back), and
  the Unload jobs that have not picked up yet. An Unload already carrying the output finishes its delivery. Collapse
  cancels them as well (GRV-07 `CancelJobsNaming`), including an Unload already carrying the output (its first step
  names the workshop, as a pump haul's does): that dwarf drops the output as a pile, which is hauled to storage.
  No job counts as failed. crafting.md CRF-10 is updated.
- **Preemption withdraws.** A Craft or Unload job released by JOB-07 preemption is removed, like a need job, not
  returned to the board. Its pick-up is spent and a Flee preempts during the agents step, after `Workshops.Tick`, so
  a stale copy with steps back at 0 could be claimed by another dwarf in the same tick and craft its cycles again
  (sim-reviewer). `Workshops.Tick` posts a fresh plan next tick. A save that holds a Keep order with done > 0 or a
  Make order with done >= count is refused as corrupt.
- **The craft check is the stand cell.** CRF-11 "the agent is on its entrance" is checked as `a.Cell ==
  Construction.StandCell(workshop)`, which is the entrance on flat ground and the step the Craft job walks to.
- **Regions.** The CRF-09 source must be in the region of the workshop's stand cell; before the first region build
  (region None) any source counts, as elsewhere.
- **Plan changes re-post.** An unclaimed Craft job is compared with the current plan (steps and reservations) every
  tick and withdrawn and re-posted on any difference. An unclaimed Unload job is re-planned in place, like a pump haul.
- **Status.** `Waiting` is returned when an order wants output, has input and no Craft job is claimed (the job is
  open or will be posted next tick); `NoInput` names the first wanting order's input when no wanting order has one;
  `OutputFull` names that order's output.
- **Orders on non-workshops.** Every building saves an order list (empty unless it has a workshop block); only
  workshops hash theirs, so the other buildings hash as before (CRF-22).
- **Goldens.** The wagon's refined start stock changes seed 1 from tick 0; the goldens are regenerated (CRF-23).
Consequences: test worlds that build refined blocks stock planks or cut stone directly; `ScenarioBuilder` takes an
optional `ContentDb` for test-only workshop data. The screenshot scripts fit the wagon's 20 planks and 20 cut stone
(the materials row uses all 20 cut stone).

## ADR-084: Trade wagon: job shapes, free stock, site clearing, save layout, Dig re-goal (2026-09-27, M11-T5)
Context: M11-T5 builds CRF-15..22 and the trader events. A few points in the spec were loose, and a long scenario test
showed a pit dig race once the trader changed the dwarves' timing.
Decision:
- **Job shapes.** Pay: `GoToBuilding(source) → PickUpFromStorage(source, item, n) → GoTo(stand cell, Exact) →
  Pay(trader, deal)`, reserving `StorageOut(source)`. Unload: `GoToBuilding(trader) → PickUpFromStorage(trader) →
  GoToBuilding(storage) → DeliverTo`, reserving `StorageOut(trader)` and `StorageIn(storage)` (GoToBuilding as in
  ADR-083). Both are `JobKind.Trade`, priority 35; a pay job is the one whose last step is `Pay`. `Pay` checks the
  agent is on `Construction.StandCell(trader)` (the entrance on flat ground), as the craft check does (ADR-083).
- **Upkeep.** `Traders.Tick` (step 7, after `Workshops.Tick`) cancels Trade jobs of a gone trader or deal and
  re-plans unclaimed ones in place each tick, BLD-06 style; surplus ones are withdrawn. Pay loads: per deal, owed minus
  paid minus claimed pay counts, in loads of `CarryCapacity`; source = nearest complete non-trader storage with a
  whole load (Manhattan to the trader entrance, then lower id), else the one with the most, carrying what it has.
  Unloads: the trader's unreserved stock, lowest item id first, to the JOB-10 storage, bounded by its room.
- **Free stock.** CRF-18 "unpaid units of earlier deals" means owed units not paid and not yet taken by a claimed pay
  job: a claimed job's units are already reserved or carried, so they are out of the storage sum.
- **Site clearing.** Arrival uses the BLD-15 search (`WorldFactory.PlaceBesideHall`). The site may hold loose piles or
  dwarves; like a new building site it clears dig marks on its floor, moves piles in its footprint to its entrance
  (cancelling their pick-ups) and moves dwarves there too, rather than refusing the site. No hall means no site.
- **Departure.** Spec steps 1-4 in order; dwarves on the footprint are moved to a safe stand after it turns to Air.
- **Preemption withdraws** a Trade job, as for Craft and Unload (ADR-083).
- **Save v9.** The `Traders` section holds the trader building id (0 when none), then while a trader is here the lots
  left per offer and the deals. Load refuses a trader id that is not the trade wagon, an offer count that differs
  from the content, lots or payments out of range, `granted != paid / give`, and a trade wagon with no visit.
- **Dig re-goal.** The ADR-062 GoTo re-goal now covers Dig goals too: when a dig GoTo's goal cell stops being
  walkable (another digger dug its floor out), the step restarts and picks fresh stand cells instead of failing.
  The M4 tree-floor pit test hit this race (and a follow-on DSG-08 wait) once the trader's visit shifted the dwarves'
  drink trips; with the re-goal it runs with no failed job again.
- **Test updates.** Tests pinning `FormatVersion` 8 now expect 9 and building counts 7 now expect 8 (the data grew).
Consequences: goldens change after tick 3000 (the first arrival runs in the tick after the 3000 hash); the 6000 line
is regenerated. The trader appears in every test world with a hall that runs past tick 3000. A trader is sent away on
any tick outside its visit window, not only the exact leave tick. `BuildingVisuals.WheeledIds` includes the trader.

## ADR-085: Workshop and trade panels: order buttons, Accept all, where they sit (2026-09-27, M11-T6)
Context: VIEW-28..30 leave open how the order buttons behave on a row with no order, what "Accept all" sends, how a
toast "opens" the trade panel, and where the panels go.
Decision:
- **Order rows** (`WorkshopPanelModel`, ViewCore). The only view state is the mode picked on a row with no order
  (Make by default). + or - on such a row creates the order in the picked mode (count = the step); a step to 0 or Clear
  sends count 0 (removes it), and the row keeps its mode. Make / Keep on a row with an order re-sends it with the same
  count in the new mode, so the sim resets `done` (CRF-07). Every button sends at most one `SetWorkshopOrder`; the
  count is clamped to 0..999, and a no-op sends nothing. Keep rows show "in stock N" (CRF-08 `Economy.Stock`).
- **Status words** (CRF-13): not built yet, no orders, working, output full (item), needs item, waiting for a crafter,
  orders met. The HUD alert for a complete workshop in NoInput or OutputFull uses the same text ("Sawmill: needs log").
- **Accept all** sends the most lots the colony can pay for now (at most the lots left). When it cannot pay for one it
  asks for every lot left, so it is disabled with the sim's reason. The button reads "Accept all (n)". The panel runs
  the CRF-18 checks in the sim's order with `Traders.FreeStock`, so a disabled button is exactly a command the sim
  would refuse (tested against the sim).
- **Toolbar and toasts.** A "Trade" button at the end of the toolbar is enabled while a trader is here; its tooltip
  is "Trade wagon: leaves in 3h" or "Trade wagon in 1 day 4h" (hours of 100 ticks, rounded up). The arrival toast
  names the button ("open Trade to see its offers") rather than being clickable; TraderLeft and TraderNoRoom toast too.
- **Panels** sit beside the colonist panel; one is open at a time. A Select click on a workshop (complete or planned)
  opens its panel; a gone workshop, a departed trader or a load closes it.
- **Screenshots.** A timed `workshop` script (a stonecutter and a sawmill with orders, one accepted lot at tick 3001),
  a `workshop` camera preset, and `--panel workshop|trade` (`PANEL=` in screenshot.sh).
Consequences: no sim change; goldens unchanged. The top bar totals were already every item in data order; with the
Trade button the totals may wrap or drop below the toolbar at narrow widths (HudLayout, tested).

## ADR-086: The economy scenario: a Keep-order colony, affordable trades, a hall with an inner stair (2026-09-27, M11-T7)
Context: crafting.md scenario 10 asks for an `EconomyScript` on seed 1 that builds both workshops, keeps planks and
cut stone stocked, trades with one wagon and builds a small hall from refined blocks by day 10 with all 5 alive. The
spec leaves open the hall's shape, how many lots to accept, and where the water and the logs come from.
Decision:
- **Timed and computed**, like `WorkshopScript`: `EconomyScript.EnqueueDue` computes its commands at their ticks. Tick
  0: the monument's chop area (it covers the hall site) and its Water Pump site, then a Sawmill and a Stonecutter at the
  nearest free sites by the hall (`FindSite`, the hall site kept clear). Tick 1: Keep 30 planks and Keep 40 cut stone.
  Tick 3000: chop every tree around the hall too (the survival chop area), logs for later visits and the sawmill.
- **Trades are affordable by construction.** At fixed ticks in each visit (3001, 3600, 4200, 10201, 10800, 17401,
  18000) it accepts as many lots of offer 0 (10 log -> 10 stone) as `Traders.FreeStock(log)` pays for, if any lots
  are left. So no AcceptOffer is ever rejected (the scenario asserts no rejections). On seed 1 this is 2 lots at 3001
  and more at the later visits: 4 accepts and 4 fully granted deals over 3 visits.
- **The hall**: 5x5 at the monument's flat site, three courses: Slate, Polished stone, Wood planks (75 cut stone and
  18 planks with the stair), a two-high door in the south wall, planned then released at tick 3600. A builder reaches
  one level up or down (26-neighborhood), so a third course cannot be built from the ground: a two-step Wood planks
  stair inside against the north wall lets builders reach the wall top, as the monument's inner stair does. Without
  it the top course never builds (seen: 30 of 46 blocks).
- **Stone** comes from the starting wagon (60) and trades; no quarry is dug (the M11-T8 stair dig is not needed).
- **Profiling.** `TickPhase` gains `Workshops` and `Traders` (step 7), so CRF-P1 prints their totals. Profiling only;
  no sim state or tick order changes.
Consequences: the hall is done at tick 7801 (day 3); both Keep orders are met (1599 and 1696) and are met again at
day 10. CRF-P1 measures ticks 3700..4199 (trader in all 500 ticks, both workshops working): median about 0.8 ms of 8.
Goldens unchanged.
