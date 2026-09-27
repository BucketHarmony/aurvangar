# Spec — View, input, UI (VIEW)

All of this lives in `src/Aurvangar.Godot`. It reads sim state and sends commands. No gameplay rules here.

## Game loop

- **VIEW-01** `GameRoot._Process(delta)`: accumulate `delta * speedMultiplier`; while accumulator ≥ 0.1 s and
  ticks this frame < 4, call `sim.Tick()` and drain events. Speed multipliers: 0 (pause), 1, 3, 6.
  Keys: Space = pause toggle, 1/2/3 = speeds.
- **VIEW-02** After draining events, process up to 4 queued chunk remeshes and 4 water remeshes per frame.

## Terrain rendering

- **VIEW-03** `ChunkMesher` (pure C#, lives in `src/Aurvangar.ViewCore/Meshing/`, no Godot types, unit-tested)
  produces greedy-meshed quads per face direction per block type for a 32³ chunk, reading neighbors across
  chunk borders. Output: vertex, normal, color arrays; one `ArrayMesh` surface per chunk with vertex colors from
  `data/palette.json`. Collision: a `ConcavePolygonShape3D` per chunk from the same quads (used only for mouse
  picking).
- **VIEW-04** **Z-slicing** (DF-style "view level"): `SliceY` defaults to `SizeY - 1`. The mesher treats every
  cell with `y > SliceY` as Air and emits top faces of solid cells at `y == SliceY` with a darkened "cut" color.
  PageUp/PageDown or `[`/`]` change `SliceY` by 1; changing it remeshes only chunks whose Y range contains the old
  or new slice. Agents, buildings, water, and piles above `SliceY` are hidden.
- **VIEW-05** Picking: mouse ray against chunk collision returns the hit cell and face normal. With slicing on,
  picks above `SliceY` are ignored.

## Camera

- **VIEW-06** Orbit rig: WASD/arrow pan on the XZ plane, Q/E rotate 90° steps with a 0.2 s tween, mouse wheel
  zoom (distance 10–120), middle-drag orbit pitch clamp 25°–80°. Camera focus follows slice level height.
  Right-drag orbits exactly like middle-drag (M7-T1, ADR-054). A right press becomes a drag once the pointer has
  moved more than 4 px (net) from the press; a right click released before that aborts the tool drag in progress.

## Water, agents, buildings, piles

- **VIEW-07** Water surface per chunk as described in `water.md` rendering contract. Semi-transparent blue,
  darker with depth.
- **VIEW-08** Agents: capsule mesh, position lerped between `cell` and `nextCell` by `moveProgress`. Color by
  state (idle grey, working white, dead red). A tiny carried-item cube in the item's palette color.
- **VIEW-09** Buildings: box of footprint size. Blueprint = translucent wire color; under construction = semi
  opaque with progress bar label; complete = solid palette color. Pump shows a `NoWater` icon when flagged.
- **VIEW-10** Item piles: one fixed-size marker per pile whatever its count (a crate most of a cell wide in the
  item's color with a lighter cap), readable at the default zoom, with a count label above it shown at camera
  distances up to 80 (hidden at the overview). A pile over a dug floor stays in its cell and gets a thin post down
  to the floor below (at most 8 cells); hovering that floor finds the pile. Item name and count on hover (M4-T16).
- **VIEW-11** Designations: dig = translucent orange cube per cell; chop = orange ring on tree; farm = brown
  overlay (raised furrows; crops on them grow in 8 stages, straw colored when dry, potato caps when mature; M6-T5,
  ADR-050); unreachable = red tint.

## Tools and input

- **VIEW-12** Toolbar buttons with hotkeys: Dig (G), Chop (C), Farm (F), Build ▸ Warehouse / Pump / Levee (B),
  Deconstruct (X), Cancel (Z). Esc returns to Select. M8-T5 adds Blocks (K) and Release plan (L) (VIEW-21).
- **VIEW-13** Dig tool: drag defines a box from the first picked cell to the second, Y range from the first
  pick's cell down to `SliceY` level of the second pick (so dragging on a sliced layer digs that layer). Sends
  `DesignateDig(box)`.
- **VIEW-14** Build tool: ghost follows the cursor snapped to the grid; R rotates; ghost is green when the sim's
  `BuildingSystem.CanPlace(def, origin, rot)` returns Ok, red otherwise with the reason in a tooltip. Click sends
  `PlaceBuilding`. Shift keeps the tool active (levee lines by dragging). The mouse label (this tooltip, pile counts,
  farm hints) never covers a building billboard and stays on screen; building labels that overlap each other are
  lifted apart (M7-T7, ADR-060).

## Block construction (M8, `construction.md`)

- **VIEW-21** Block tool (hotkey K, toolbar "Blocks"). Single blocks since M9-T1 (G4 answer "building should be one
  square at a time", ADR-067, which amends ADR-065):
  - A block picker lists the construction blocks from `ContentDb` by their `label`. There are no shape modes and no
    height control in the player's tool; the sim keeps the `DesignateBuild` shapes for scripts and tests (CON-07).
  - A click on a block face plans or places one block in the cell that face looks into (`PickHit.Adjacent`): a top
    face stacks upward, a side face places beside.
  - A drag paints one block into each new cell the cursor passes over (`PaintDrag`). The cells stay on the layer of
    the first cell: the cursor ray is cut with the horizontal plane of the first picked face (the face's own plane
    for a top or bottom face, mid-layer for a side face). Between two cursor cells the path is 4-connected, so
    painted neighbours share a face. A wall is painted course by course. At most 1,024 cells per drag; a cursor
    jump of more than 64 cells is ignored.
  - The ghost shows the painted cells (or, with no drag, the one cell a click would place) in the block's palette
    colour. A cell that fails `BlockPlans.CanPlan` (with the painted cells as `pending`) is red, and its reason is in
    the mouse label (`LabelLayout.PlaceTooltip`).
  - P toggles plan mode. The tool label then shows "Plan", and the commands are sent with `Plan = true`.
  - Releasing the drag sends one `DesignateBuild(Single, c, c, 1, block, plan)` per valid cell, in support order
    (each cell after a neighbour below or beside it that is solid, planned or earlier in the order). With no valid
    cell nothing is sent and the reason is shown. The tool stays active after a release (Esc leaves it; a right
    click drops the drag).
  - A "Release plan" tool (hotkey L) sends `ReleasePlan` for a dragged box. Its "Release all" button sends one over
    the whole world.
  - The Deconstruct tool (X): a click on a building is BLD-09. Otherwise a click marks the built block under the
    mouse, and a drag paints the built blocks on the first block's layer, the same way. The release sends one
    `DesignateDeconstructBlocks(c, c)` per built block. Marked blocks show orange.
  - All of this logic (face to cell, paint path, verdicts, order, commands) lives in ViewCore with unit tests.
- **VIEW-22** Plan entries render as translucent block-sized ghosts in the block's palette colour:
  - `Released` entries at alpha 0.45;
  - `Planned` entries at alpha 0.25, lightened;
  - entries whose status is `GivenUp`, `NoAccess`, `WouldStrand` or `NoSupport` tinted red.

  Ghosts above `SliceY` are hidden. Built blocks mesh as terrain (VIEW-03) with their palette colours. Hovering an
  entry shows its label and status text ("Stone wall: waiting for the block below").
- **VIEW-23** While any entry exists, the top bar shows the plan's material against stock.
  - The format is "Building: stone 120/64 · Planned: stone 70, log 12". It uses `BlockPlans.Needed(Released)` and
    `Needed(Planned)` against `Storage.Totals`.
  - An item whose need exceeds its stock is orange.
  - The text is built in ViewCore (`TopBarModel.PlanText`).

## HUD

- **VIEW-15** Top bar: day, season + days left ("Wet season, 4 days left", orange in a drought, toast on change;
  ADR-050), speed, totals for log/stone/berries/potato/water. Alerts: no food, no water, pump has no water, and
  "Unreachable: Water Pump, log pile x2" naming every JOB-12 given-up job source (M7-T5).
- **VIEW-16** Colonist panel (left): name, hunger/thirst/health bars, current job label. Click centers camera.
- **VIEW-17** F3 debug overlay: FPS, sim ms per tick (avg over 60), water active cells, water step ms,
  path searches/s, region rebuild ms, open jobs by kind.
- **VIEW-18** "Colony lost" modal on `ColonyLost` with a Load button.
- **VIEW-19** F5 quick save to `user://quick.save`, F9 quick load.

## Screenshot harness

- **VIEW-20** `scenes/Screenshot.tscn` + `ScreenshotRunner.cs`: reads command-line user args
  (`-- --seed 1 --ticks 1200 --shots overview,river,hub --out artifacts/screens`), runs the sim without
  rendering waits, positions the camera at named presets, renders, saves PNGs, quits. Used by
  `scripts/screenshot.sh`. An optional `--script` makes colonists work in the shots; `survival` is timed (each command
  enqueued at its tick, M7-T7), with the `tunnel` and `reservoir` presets for its hill tunnel and levee reservoir.
