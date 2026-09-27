# Spec — Gravity for piles and buildings (GRV)

G6 follow-up: "buildings, items, log piles with no ground beneath them should fall down to ground. Physics should be
explored." M11-T9, ADR-079.

Scope: loose item piles and prefab buildings. Terrain never caves in, and built blocks keep the CON-09 support rule
(they never float, so they never fall). Agents never fall: a dig never removes an agent's floor (JOB-09, `Dig`
Blocked), and a collapse moves the agents on it off first (GRV-07).

Terms:
- **Rest cell** of a non-solid cell `c`: go straight down from `c` while the cell below is not solid; the cell where
  that stops (or `y = 0`). A cell is its own rest cell when the cell below it is solid.
- **Anchored** building: `prebuiltOnly` and not `removableWhenEmpty` (the Great Hall). It can never be torn down or
  undermined.

## The gravity step (GRV-01..02)

- **GRV-01** A pile **rests** when the cell below it is solid (any solid block: terrain, built block,
  BuildingSolid) or it is at `y = 0`. A building **stands** when at least one cell under its bottom layer is solid or
  is covered by another building (any state; a blueprint levee stacked on a blueprint levee stands, BLD-04). Partial
  support is enough: a warehouse on one of its four floor cells stands. A built block of any fine shape (slab, stair, pillar, CON-19) is a
  solid cell like a full one (CON-21, ADR-080): piles rest on it and buildings stand on it.
- **GRV-02** `Gravity.Tick` runs at ARCH-01 step 10b: after `AgentSystem.Tick` (step 10, where every dig and teardown
  happens) and before `WaterGrid.EndTick`, so the blocks a collapse removes are seen by water (WAT-13), the path grid
  and regions in the same tick. It changes nothing when everything rests and stands.
  1. Buildings, in ascending id: every building that is not anchored and does not stand collapses (GRV-07). This
     repeats until a pass finds none (a cascade, GRV-08).
  2. Piles, in ascending cell index (collected first, then moved): every pile that does not rest falls (GRV-03).

## Piles (GRV-03..04)

- **GRV-03** A falling pile moves **at once** to its rest cell, in the same tick. There is no in-flight state (so
  nothing new to save or hash). It lands on the rest cell when that cell takes the item (empty, or the same item:
  the stacks merge, ECO-08) and lies outside every building footprint. Otherwise it is placed by ECO-08 from the rest
  cell (`PlacePile`: the nearest free standable cell within radius 3, spiral order), else the first spiral cell that
  rests, is not solid and takes the item. Items are conserved. A pile with nowhere to land stays where it is and
  tries again at the next gravity step (items dropped by a collapse are never kept: they go to `PlacePile`).
- **GRV-04** Jobs that would still pick the pile up at its old cell (a `PickUp` step there not yet run by the
  claiming agent, or any unclaimed one) are cancelled, not failed (no JOB-08 count). HaulSystem posts a new haul for
  the landed pile at its next step.

Felled logs (ECO-09), dig drops, CON-17 refunds, BLD-09 refunds and piles pushed out of a site are ordinary piles:
they fall the same way. A dig of a cell with air below it (a cave roof, a block of a bridge) leaves its drop falling.

## Buildings (GRV-05..09)

- **GRV-05** The ground under an anchored building can never be dug: DSG-02 does not mark it and `WorldActions.Dig`
  returns `Blocked`. So the Great Hall always stands.
- **GRV-06** Undermining. The **floor** cells (the cells right under the bottom layer) of a **complete**, not
  anchored building may be marked (DSG-02) and dug. The floor of a blueprint, a construction site or a building
  being deconstructed may not: DSG-02 does not mark it, and `Support.Depends` counts it as depending (GRV-09), so no
  Dig job is posted for a mark there, a Dig job posted earlier waits (stand-down, not a failure), `Dig` returns
  `Blocked`, and `DigStatus` reports `Support`. (Placing a blueprint still clears the marks on its floor, ADR-041.)
- **GRV-07** A collapse:
  - A blueprint or construction site is cancelled as by BLD-09 (its jobs cancelled, delivered materials refunded as
    piles at its stand cell). The refund falls if it must (GRV-03).
  - A complete building or one being deconstructed:
    1. Every job that names it (a step on it: go to, work on, take from, deliver to or consume from it; or a storage
       reservation on it) is cancelled, not failed.
    2. Its footprint turns to Air (buildings that set blocks).
    3. Living agents that stand in, or step into, a footprint cell or a cell right above its top are moved (as
       BLD-07) to its stand cell if it is now standable and outside the footprint, else to the first such cell of the
       ECO-08 spiral around the stand cell's rest cell, else to that rest cell.
    4. Half its cost, rounded down (as BLD-09), then everything stored, each in ascending item id, fall from its
       origin cell (the bottom layer): each lands per GRV-03 from the origin's rest cell. So a collapsed warehouse
       lies as piles in the pit under it.
    5. It is removed (`BuildingRemoved`).
- **GRV-08** Cascade. A building that stood only on a collapsed one collapses in the next pass of the same step. A
  levee stack whose bottom levee is undermined comes down whole, each levee dropping its own refund (1 log each).
  A warehouse on levees comes down with them.
- **GRV-09** CON-10 still holds. Removing a cell also removes, in the what-if, every building that would collapse
  because of it (with the cascade, `Gravity.WouldCollapse`). `Support.Depends(sim, cell)` counts their footprints as
  removed, so a dig whose collapse would unground a built block waits (`DigStatus` `Support`) and `Dig` returns
  `Blocked`, like any CON-10 dig.

## Determinism and save (GRV-10)

- **GRV-10** Buildings are visited in ascending id and piles in ascending cell index; collapses and pile moves use
  only integer state. There is no new state: falls are instant and collapses are immediate, so `StateHash` and the
  save format are unchanged (`FormatVersion` stays).

## Acceptance scenarios

1. A log pile on a dirt floor: dig the floor and the pile lies on the next solid floor below, in the same tick.
2. A stocked warehouse: dig out the ground under it; it stands until the last floor cell goes, then collapses. Half
   its cost and its stock lie in the pit and are hauled to the hall; nothing floats (Grounding helper).
3. A two-levee stack: undermine the bottom levee and both come down, with 2 logs in one pile.
4. The wagon collapses like a warehouse; the ground under the Great Hall cannot be dug.
5. A built block resting on a building: the last floor dig waits (CON-10).

## Task map

| Task | Implements |
|---|---|
| M11-T9 | GRV-01..10 |
