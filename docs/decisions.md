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
