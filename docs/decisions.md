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
dug and 2 turned red. Other dwarves are not protected by this rule (only the digger); in pits they stay connected
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
