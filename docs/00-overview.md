# 00 — Overview and scope

## The game

**Aurvangar.** The name comes from the Völuspá's list of dwarves: Dvalin's people "went from the stone of the halls
to the seat of Aurvangar," the mud-plains (*aurr*, wet clay, plus *vangr*, field). A dwarf hold leaves its mountain to
found an outpost on wet low ground beside a river, far into the wild. The river is what keeps the outpost alive and
what drowns it when the dwarves dig carelessly.

Theme rules for all player-facing text: the colonists are **dwarves**; the hub is the **Great Hall**. Code
identifiers stay generic (`Agent`, `hub`) so the sim does not care about flavor. Dwarf names come from the Old Norse
Dvergatal (public domain). Nothing from Tolkien's invented languages or text.

## Long-term direction (not in the POC; do not build)

Once a hold is stable, the endgame is an outpost's purpose: **science** (studying the wild), **exploration** (beyond
the starting map) and **diplomacy** (with whoever else lives out there), in the vein of RimWorld's late game. The
POC proves the foundation those depend on: a fortress that survives its river. No backlog task may add research,
world-map, or faction systems until a later milestone plan says so.

## What this POC proves

A small voxel colony sim where water is the system the player plans around. Dwarves are directed indirectly
(designations and prefab buildings, Timberborn style). Terrain is fully voxel and diggable (Dwarf Fortress
style). Water is a cellular automaton that floods what you dig and dries up in droughts.

M8 (after gate G3, ADR-061) adds **free-form construction**. The player paints walls, floors, boxes and stairs block
by block, or lays out a monument as a plan and releases it. Dwarves quarry, fetch and build it (`specs/construction.md`).
Prefab buildings stay for functional buildings.

The POC is done when the **definition-of-done session** below plays end to end on seed `1` and all tests are green.

## Definition-of-done session (seed 1)

1. The map loads: 128×128×64 voxels, a hill, a river flowing from the west edge to the east edge, trees, berry
   bushes. A pre-built Great Hall (the `hub` building) stands on a flat patch near the river with 5 dwarves and starting stock
   (40 berries, 30 water, 30 logs).
2. The player places a Water Pump on the riverbank. Colonists build it. It fills storage with water.
3. The player designates a farm field near the river. Colonists plant; potatoes grow only on moist tiles.
4. The player designates chopping; colonists fell trees and haul logs to storage.
5. The player places a Warehouse and colonists build it.
6. The player digs into the hill to reach stone. Colonists dig and haul stone.
7. The player digs through the riverbank. Water floods the tunnels. Colonists caught in deep water path out
   (or cannot enter). The player walls the breach with Levees and the flood stops spreading.
8. A drought arrives (the river source stops for 2 days). The river drains. Crops on dry tiles wither. Colonies
   that built a levee reservoir keep their pump running.
9. If every colonist dies of hunger or thirst, the game shows "Colony lost". There is no win screen.
10. The player can save at any point, quit, load, and continue with identical results.
11. (M8) The player plans a stone tower (at least 7×7, 8 high, hollow, with a door) and a walled courtyard, and
    releases the plan. Dwarves quarry stone from the hill and build it by day 10. No dwarf is walled in and no block
    floats (`MonumentScript`, M8-T6).

## In scope

| Area | MVP content |
|---|---|
| World | Fixed 128×128×64, 32³ chunks, 8 terrain/building block types plus 3 construction blocks (M8), seeded terrain with hill + river channel |
| View | RTS orbit camera, z-level slicing, greedy-meshed chunks, water surface mesh, placeholder building meshes |
| Agents | 5 colonists, no births. Needs: hunger, thirst. Death at zero after a grace period |
| Movement | A* on voxel grid, 1-block step up/down, 8-way with no corner cutting, deep water impassable |
| Jobs | Dig, Chop, Haul, Deliver, Construct, Deconstruct, Plant, Harvest, OperatePump, Eat, Drink, Build (M8) |
| Buildings (prefab) | Great Hall `hub` (pre-placed), Warehouse, Water Pump, Levee |
| Designations | Dig (box), Chop (area), Farm field (area), Cancel, Deconstruct, Build blocks (shapes), Deconstruct blocks (M8) |
| Construction (M8) | Stone wall, wood planks, polished stone. Shapes: single, line, wall, floor, hollow box, stair. Support rule (no floating blocks), bottom-up build order, no walling a dwarf in, plan layer with release and material totals |
| Water | Fixed-point CA, active-cell update, sources and drains at map edges, drought schedule |
| Farming | Moisture map from nearby water; potatoes grow only when moist; wither when dry too long |
| Economy | Items: log, stone, berries, potato, water. Storage in Hub and Warehouse. Items on ground get hauled |
| Time | 10 Hz fixed tick. Pause / 1× / 3× / 6×. Day = 2400 ticks (4 min at 1×) |
| Persistence | Save/load of complete sim state; replay-equal after load |
| UI | Toolbar, resource bar, colonist panel, day/weather readout, debug overlay (F3), "Colony lost" screen |

## Explicitly out of scope

Combat, hostile mobs, animals, births and population growth, housing and sleep, moods, skills, research,
mechanical power, processing chains (sawmill, cooking), trade, multiplayer, modding API, lighting simulation,
temperature, water pressure and currents, paths/roads as buildings, stairs and ladders as movement features (a
staircase built from blocks is fine: dwarves climb it one step at a time), tree regrowth, audio beyond placeholder
clicks, Steam integration, settings menu, localization, save-format migration.

Construction (M8) is out of scope for these: scaffolding, doors as blocks (a door is a gap), slopes and half
blocks, block rotation, structural collapse or cave-ins, copy/paste and plan import/export, and blocks placed without
a dwarf.

A task that needs any of these is out of scope. Record the gap in `docs/decisions.md` and move on.

## Coordinate convention

Sim and view both use **Y up**. `Int3(X, Y, Z)`: X east, Y up, Z south. World extents `X∈[0,128)`, `Y∈[0,64)`,
`Z∈[0,128)`. One voxel = one Godot unit. This avoids a swizzle at the boundary.

## Glossary

- **Cell** — one voxel position. **Block** — the solid material in a cell (or Air).
- **Standable cell** — an air cell with a solid (or building-floor) cell below and air above; agents occupy these.
- **Designation** — a player mark on cells that generates jobs (dig, chop, farm).
- **Building** — a prefab with a footprint, placed as a blueprint, becomes a construction site, then complete.
- **Built block** — a cell holding a construction block (stone wall, planks, polished stone) that dwarves placed.
- **Plan entry** — a cell the player wants filled with a construction block, `Planned` (on paper) or `Released`
  (dwarves build it).
- **Tick** — one sim step (100 ms of game time at 1×).
- **Water unit** — fixed-point water volume. 1 full cell = 1024 units.
