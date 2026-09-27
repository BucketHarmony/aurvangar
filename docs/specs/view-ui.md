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
  `DesignateDig(box)`. M11-T8: this is the Box mode; Stair down is VIEW-24.
- **VIEW-14** Build tool: ghost follows the cursor snapped to the grid; R rotates; ghost is green when the sim's
  `BuildingSystem.CanPlace(def, origin, rot)` returns Ok, red otherwise with the reason in a tooltip. Click sends
  `PlaceBuilding`. Shift keeps the tool active (levee lines by dragging). The mouse label (this tooltip, pile counts,
  farm hints) never covers a building billboard and stays on screen; building labels that overlap each other are
  lifted apart (M7-T7, ADR-060).
- **VIEW-24** (M11-T8, ADR-078) Dig tool modes: **Box** (VIEW-13) and **Stair down**, chosen with T while the dig
  tool is active or with the dig options row (bottom left, shown only with the dig tool; the mode is kept when the
  tool changes). A stair drag digs a 1-wide staircase: the direction is the drag's main XZ axis (X on a tie, +X when
  the drag does not move), the bottom is the second pick's cell clamped to `SliceY` and never above the first cell,
  and step k (k = 0..first.Y - bottom) is the cell `first + k*dir - k*Up` with the two cells above it (a column of 3,
  so a dwarf climbs each step: PTH-01/05). Release sends one `DesignateDig` per column (no new command). While held,
  the solid cells it would dig show as orange marks and the mouse label reads "Stair down N levels to level Y" (or
  asks for a lower view level when N is 0). Logic: `StairDig` and `ToolController` (ViewCore).
- **VIEW-25** (M11-T8) Hovering a dig mark at or below `SliceY` with any tool shows why it waits (DSG-10): "Dig:
  would trap a dwarf (a dwarf climbs 1 level; dig a stair down, T)", "Dig: waiting for the cell above", "Dig:
  unreachable, no dwarf can get to it", and the other `DigWait` reasons. It is computed at most once per hovered
  cell, tick and slice. Logic: `DigHover` (ViewCore).
- **VIEW-26** (M11-T8) A line at the bottom right shows the view level and its keys: "View level: top (63) ·
  PageUp/PageDown or [ ] to slice", or "View level: S of 63 (N down) · PageUp/PageDown or [ ] to move". Logic:
  `SliceHint` (ViewCore).

## Block construction (M8, `construction.md`)

- **VIEW-21** Block tool (hotkey K, toolbar "Blocks"). Single blocks since M9-T1 (G4 answer "building should be one
  square at a time", ADR-067, which amends ADR-065):
  - A block picker lists the construction blocks from `ContentDb` by their `label` (fine shapes: VIEW-27). There are no shape modes and no
    height control in the player's tool; the sim keeps the `DesignateBuild` shapes for scripts and tests (CON-07).
  - A click on a block face plans or places one block in the cell that face looks into (`PickHit.Adjacent`): a top
    face stacks upward, a side face places beside.
  - A drag paints one block into each new cell the cursor passes over (`PaintDrag`). A drag that starts on a top or
    bottom face stays on the layer of the first cell: the cursor ray is cut with the face's own horizontal plane. A
    drag that starts on a side face (M10-T3, ADR-073) stays in that face's vertical plane (the plane of the first
    cell, parallel to the face): the cursor ray is cut with the picked face's own plane, so a column, or a wall face
    the cursor sweeps over, is painted in one drag. Between two cursor cells the path is 4-connected in the plane, so
    painted neighbours share a face. At most 1,024 cells per drag; a cursor jump of more than 64 cells is ignored.
  - The ghost shows the painted cells (or, with no drag, the one cell a click would place) in the block's palette
    colour. A cell that fails `BlockPlans.CanPlan` (with the painted cells as `pending`) is red, and its reason is in
    the mouse label (`LabelLayout.PlaceTooltip`). Red cells use `InvalidCellStyle` (M9-T3, ADR-069): a strong red
    fill with a dark outline, a little larger than the cell and drawn without a depth test, so one inside or behind a
    block shows. The mouse label keeps clear of the ghost's screen box.
  - Material shortage while dragging (M10-T2, ADR-072): the ghost is compared with the free stock (`FreeStock`:
    storage totals minus the Released entries' need; in plan mode also minus the Planned need, never below 0). The
    valid cells in paint order take the free units; valid cells beyond them are amber with a darker outline
    (`ShortCellStyle`, `designations.short`), depth-tested. The valid cells take the stock bottom-up, then in paint
    order (the order they are sent in), so on a painted wall face the top cells are the short ones (M10-T3). They are short, not invalid, and are still sent. The mouse
    label adds "40 stone free" or "Only 3 stone free: 2 blocks short (amber)" ("free after the plan" in plan mode). The
    view takes the free stock with the VIEW-23 line, at most every 10 frames (CON-06).
  - P toggles plan mode. The tool label then shows "Plan", and the commands are sent with `Plan = true`.
  - Releasing the drag sends one `DesignateBuild(Single, c, c, 1, block, plan)` per valid cell, bottom-up and then in
    support order (each cell after a neighbour below or beside it that is solid, planned or earlier in the order). With no valid
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
  - Released entries whose status is `GivenUp`, `NoAccess`, `WouldStrand` or `NoSupport` red with a dark outline
    (`InvalidCellStyle`, M9-T3). `WaitSupport` (M10-T1) is not red: the plan will support the entry.

  Ghosts above `SliceY` are hidden. Built blocks mesh as terrain (VIEW-03) with their palette colours. Hovering an
  entry shows its label and status text ("Stone wall: waiting for the block below").
- **VIEW-23** While any entry exists, the top bar shows the plan's material against stock.
  - The format is "Building: stone 120/64 · Planned: stone 70, log 12". It uses `BlockPlans.Needed(Released)` and
    `Needed(Planned)` against `Storage.Totals`.
  - An item whose need exceeds its stock is orange.
  - The text is built in ViewCore (`TopBarModel.PlanText`).

- **VIEW-27** Fine shapes (M11-T11, CON-19, ADR-081). Everything below lives in ViewCore with unit tests.
  - `ShapePattern` turns a form into a 4x4x4 pattern of 0.25 m sub-cells (one bit each in a `ulong`): Full all,
    Slab the lower half, Stair the lower half plus the upper half on the high side (rotation 0 climbs toward +Z,
    1 +X, 2 -Z, 3 -X), Pillar the middle 0.5 x 0.5 post.
  - `ChunkMesher` counts a shaped cell as not solid for the full cells around it, so they draw the faces the shape no
    longer covers. It then draws each shaped cell (at or below the slice) with `ShapeMesher`: a sub-cell face shows
    where the sub-cell beside it is empty, in the same cell or across the side in the neighbour (a full solid
    neighbour covers the whole side; above the slice is air). Faces of one layer merge greedily in the 4x4 grid, so a
    slab or pillar is 6 quads. A shaped cell on the slice with a solid cell above has its top-plane faces cut
    (VIEW-04). MESH-P1 also holds with a surface chunk full of shapes.
  - Picking steps 0.1 cell behind the hit face (`PickResolver.Depth`), less than a sub-cell, so a hit on a slab's top
    or a stair's riser resolves to the shaped cell.
  - The block tool has a shape picker in its options row (the data's shape labels: Block, Slab, Stair, Pillar), V for
    the next shape, and R (and a Rotate button, shown for a stair) for a quarter turn. A shape with one rotation
    always sends rotation 0. Every painted cell takes the tool's form (`DesignateBuild.Form`). The ghost's valid cells
    and the plan ghosts draw the shape; red and amber cells stay whole cubes. Labels read "Wood planks slab" or
    "Slate tiles stair, climbing east" (+Z is south, +X east).
  - The ghost's cost, the shortage (amber) and the tooltip use the shaped cost `CostOf(block, shape)` (CON-20).
  - Limitation: the sim treats every shape as a full solid cell (CON-21). A dwarf walks on the top of the cell, so it
    stands 0.5 above a drawn slab and floats beside a pillar.

- **VIEW-28** Workshop panel (M11-T6, CRF-06..13). Clicking a complete or planned workshop opens a panel:
  - its name and status (CRF-13), for example "Sawmill: needs log";
  - one row per recipe ("Saw planks: 1 log -> 2 planks"), with its order: mode (Make or Keep), count (- and +, steps
    of 1, 5 with Shift), and `done/count` for Make. Setting, changing or clearing a row sends one `SetWorkshopOrder`
    (count 0 removes).
  - Model and texts in ViewCore (`WorkshopPanelModel`), tested.
- **VIEW-29** Trade panel (M11-T6, CRF-15..21). While a trader is here, a toolbar button and a toast on arrival open a
  panel:
  - "Trade wagon: leaves in 3h" (game hours, 100 ticks each);
  - one row per offer: "10 log -> 10 stone", the lots left, what the colony has of the give item (free stock,
    CRF-18), and Accept 1 or Accept all buttons, disabled with the CRF-18 reason as tooltip;
  - the open deals with their paid and granted lots.

  With no trader in, the button's tooltip gives the next arrival ("Trade wagon in 1 day 4h"). Model and texts in
  ViewCore (`TradePanelModel`), tested.
- **VIEW-30** HUD (M11-T6): the top bar totals include every item in data order (planks and cut stone after water);
  workshop `NoInput` and `OutputFull` statuses join the VIEW-15 alerts; the trader is drawn as a wagon (the M11-T2
  wheels and cover) in its own palette colour.

## HUD

- **VIEW-15** Top bar: day, season + days left ("Wet season, 4 days left", orange in a drought, toast on change;
  ADR-050), speed, totals for log/stone/berries/potato/water. Alerts: no food, no water, pump has no water, and
  "Unreachable: Water Pump, log pile x2" naming every JOB-12 given-up job source (M7-T5). The bar sits at the top
  right and never overlaps the toolbar (M9-T3, ADR-069): its items wrap (the first row by item, the VIEW-23 plan line
  between items) to the width between the toolbar and the right edge; if one item is wider than that, the bar moves
  below the toolbar. The layout is `HudLayout.ArrangeTopBar` (ViewCore).
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
