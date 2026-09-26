# Spec — Buildings (BLD)

Timberborn-style prefabs. Buildings are placed as blueprints on a grid, become construction sites, receive
materials, are built by colonists, then operate. Definitions live in `data/buildings.json`.

## Definition schema

```json
{
  "id": "warehouse",
  "name": "Warehouse",
  "footprint": [2, 2, 2],          // x, y (height), z at rotation 0
  "entrance": [0, 0, -1],          // offset from origin to the standable cell agents use; rotates with the building
  "cost": { "log": 20 },
  "buildTicks": 300,
  "placement": "ground",           // ground | waterEdge
  "storage": { "capacity": 150, "perItemCapacity": 0, "accepts": ["log", "stone", "berries", "potato"] },
                                   // capacity = total cap (0 = none); perItemCapacity = cap per item (0 = none)
  "workers": 0,
  "producer": null,                // pump: { output, cycleTicks, minIntakeLevel, unitsPerCycle, buffer, haulAt }
  "setsBlocks": true,              // footprint cells become BuildingSolid on completion
  "prebuiltOnly": false            // true for the hub: cannot be placed by the player
}
```

## MVP buildings

| id | Footprint | Cost | Build ticks | Function |
|---|---|---|---|---|
| `hub` | 3×2×3 | – (pre-placed, complete) | – | Storage: every item, 100 each. Spawn point. Eat/drink source |
| `warehouse` | 2×2×2 | 20 log | 300 | Storage: 150 total, solid goods only |
| `pump` | 2×1×1 | 12 log | 200 | `waterEdge`. 1 worker. Produces 1 water per 30 work ticks while intake level ≥ 256; output goes to its internal buffer (10), hauled to storage |
| `levee` | 1×1×1 | 2 log | 40 | Becomes BuildingSolid. Blocks water. Stackable (can be placed on top of another levee) |

## Placement (BLD-01..04)

- **BLD-01** Rotation ∈ {0, 90, 180, 270}. Footprint and entrance offset rotate about the origin cell.
- **BLD-02** `ground` placement: every footprint cell is Air and has no plant, no building, no agent-reserved
  construction; every cell directly below the bottom layer is solid (natural block or BuildingSolid of a levee);
  the entrance cell is standable (PTH-01). Water in footprint cells is allowed at blueprint time; it is pushed out
  on completion (WAT-12).
- **BLD-03** `waterEdge` placement (pump): as `ground`, plus the cell in front of the pump's intake side
  (the side opposite the entrance) must be non-solid; the intake cell is that cell one level down. The pump
  reads `Water.GetLevel(intake)` when producing.
- **BLD-04** Levee may be placed on top of a completed or blueprinted levee (placement checks the stack
  ordering: construction of an upper levee cannot start until the one below is complete).

## Construction (BLD-05..09)

- **BLD-05** `PlaceBuilding` creates a `Building` in state `Blueprint` with `delivered = {}`, `progress = 0`.
  Footprint cells are marked reserved (no other blueprint may overlap).
- **BLD-06** While any cost item is under-delivered, `BuildingSystem` keeps exactly as many open Deliver jobs
  as needed: `ceil(remaining / CarryCapacity)` minus already-open delivers for that site.
- **BLD-07** On first delivery the building moves to `UnderConstruction`; footprint cells become non-walkable
  (PTH-02). Agents inside the footprint at that moment are moved to the entrance cell.
- **BLD-08** When all materials are delivered, one Construct job is open at a time per site; work adds
  `progress` by 1 per tick. At `buildTicks`, the building is `Complete`: if `setsBlocks`, footprint cells
  become BuildingSolid (WAT-12 water push, PathGrid dirty, Regions dirty).
- **BLD-09** Cancel before completion refunds 100% of delivered materials as a pile at the entrance.
  Deconstruct after completion: Deconstruct job (half build ticks), then footprint cells revert to Air and 50% of
  cost (rounded down) is dropped as piles at the entrance.

## Storage (BLD-10..12)

- **BLD-10** Storage buildings hold `Dictionary<ItemId,int>` contents plus `Reserved` in/out counts. Iterate by
  `ItemId` order when hashing or saving.
- **BLD-11** Water is storable only in `hub` (and later tanks). Warehouses reject water.
- **BLD-12** Global `Storage.Totals` is recomputed each tick for the HUD (sum over storage buildings).

## Pump production (BLD-13..14)

- **BLD-13** A complete pump with internal buffer < 10 keeps one OperatePump job open. The worker stands at the
  entrance and does `Work` in 30-tick cycles; each cycle checks intake level ≥ 256 and, if so, removes 64 units
  from the intake cell (water leaves the world; counted in `WaterStats.Pumped`) and adds 1 water to the buffer.
  If intake is too low, the cycle produces nothing and the building flags `NoWater` (HUD icon).
- **BLD-14** When the buffer is ≥ 5, a Haul job moves water to the hub.

## Acceptance scenarios

1. Placement validation: overlapping blueprints rejected; floating placement rejected; pump away from water rejected.
2. Full construction of a warehouse by 2 agents from hub logs; final state `Complete`, footprint cells BuildingSolid.
3. Levee in the river: after completion the downstream cell level drops within 200 ticks.
4. Pump with a worker adds water to hub storage; pump on a dried bank flags `NoWater`.
5. Deconstruct refund math.
