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

## Water, agents, buildings, piles

- **VIEW-07** Water surface per chunk as described in `water.md` rendering contract. Semi-transparent blue,
  darker with depth.
- **VIEW-08** Agents: capsule mesh, position lerped between `cell` and `nextCell` by `moveProgress`. Color by
  state (idle grey, working white, dead red). A tiny carried-item cube in the item's palette color.
- **VIEW-09** Buildings: box of footprint size. Blueprint = translucent wire color; under construction = semi
  opaque with progress bar label; complete = solid palette color. Pump shows a `NoWater` icon when flagged.
- **VIEW-10** Item piles: small stack of cubes, count label on hover.
- **VIEW-11** Designations: dig = translucent orange cube per cell; chop = orange ring on tree; farm = brown
  overlay; unreachable = red tint.

## Tools and input

- **VIEW-12** Toolbar buttons with hotkeys: Dig (G), Chop (C), Farm (F), Build ▸ Warehouse / Pump / Levee (B),
  Deconstruct (X), Cancel (Z). Esc returns to Select.
- **VIEW-13** Dig tool: drag defines a box from the first picked cell to the second, Y range from the first
  pick's cell down to `SliceY` level of the second pick (so dragging on a sliced layer digs that layer). Sends
  `DesignateDig(box)`.
- **VIEW-14** Build tool: ghost follows the cursor snapped to the grid; R rotates; ghost is green when the sim's
  `BuildingSystem.CanPlace(def, origin, rot)` returns Ok, red otherwise with the reason in a tooltip. Click sends
  `PlaceBuilding`. Shift keeps the tool active (levee lines by dragging).

## HUD

- **VIEW-15** Top bar: day, season + days left, speed, totals for log/stone/berries/potato/water.
- **VIEW-16** Colonist panel (left): name, hunger/thirst/health bars, current job label. Click centers camera.
- **VIEW-17** F3 debug overlay: FPS, sim ms per tick (avg over 60), water active cells, water step ms,
  path searches/s, region rebuild ms, open jobs by kind.
- **VIEW-18** "Colony lost" modal on `ColonyLost` with a Load button.
- **VIEW-19** F5 quick save to `user://quick.save`, F9 quick load.

## Screenshot harness

- **VIEW-20** `scenes/Screenshot.tscn` + `ScreenshotRunner.cs`: reads command-line user args
  (`-- --seed 1 --ticks 1200 --shots overview,river,hub --out artifacts/screens`), runs the sim without
  rendering waits, positions the camera at named presets, renders, saves PNGs, quits. Used by
  `scripts/screenshot.sh`.
