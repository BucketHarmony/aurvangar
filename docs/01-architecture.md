# 01 — Architecture

## Layers

```
┌──────────────────────── Aurvangar.Godot (view) ────────────────────────┐
│ GameRoot (Node) ── owns Simulation, runs fixed-tick accumulator      │
│ ChunkRenderer   ── greedy meshes per chunk, rebuilds on ChunkDirty   │
│ WaterRenderer   ── surface mesh per chunk, rebuilds on WaterDirty    │
│ AgentRenderer   ── one node per agent, interpolates between cells    │
│ BuildingRenderer── placeholder mesh per building / blueprint         │
│ CameraRig, SliceController, ToolController (input → ICommand)        │
│ Hud (resource bar, colonist panel, toolbar, debug overlay)           │
└──────────────┬───────────────────────────────▲──────────────────────┘
               │ ICommand (enqueue)            │ read-only queries + SimEvent drain
┌──────────────▼───────────────────────────────┴──────────────────────┐
│                     Aurvangar.Sim (no Godot)                           │
│ Simulation ── Tick() runs systems in fixed order                    │
│  World (VoxelWorld, chunks) · WaterGrid · MoistureMap               │
│  WorldActions (the only mutation path for agent work)               │
│  PathGrid + Pathfinder + Regions                                    │
│  JobBoard, Designations, AgentSystem, NeedsSystem                   │
│  BuildingSystem, StorageSystem, ItemPiles                           │
│  FarmSystem, PlantSystem (trees, bushes)                            │
│  WeatherSystem (drought schedule)                                   │
│  ContentDb (from data/*.json) · Rng · SimClock · EventBus           │
│  SaveGame (serialize / deserialize full state)                      │
└─────────────────────────────────────────────────────────────────────┘
```

## Tick order (ARCH-01)

`Simulation.Tick()` executes, in this exact order:

1. `ApplyCommands` — dequeue all `ICommand`s queued since last tick, apply in enqueue order, append to the
   command log (for replay and save).
2. `WeatherSystem.Tick` — advance drought schedule, set source strength.
3. `WaterGrid.Tick` — one CA step (see `specs/water.md`).
4. `MoistureMap.Tick` — recompute every 50 ticks (tick % 50 == 0).
5. `PlantSystem.Tick` — bush regrowth, crop growth / wither.
6. `NeedsSystem.Tick` — decay hunger/thirst, damage, death.
7. `BuildingSystem.Tick` — production (pump), construction completion, post jobs. M11 adds, in order after the
   pumps, `Workshops.Tick` (craft and unload jobs, CRF-10..12) and `Traders.Tick` (arrival, deals, departure,
   CRF-16..21; it may write blocks, which step 10 applies like any other change).
8. `DesignationSystem.Tick` — post jobs for new/changed designations.
9. `HaulSystem.Tick` — post haul jobs for loose item piles and full producers.
10. `AgentSystem.Tick` — each agent in ascending id order: pick job if idle, advance current job step.
    Then `WaterGrid.EndTick` — apply block changes made after step 3 (WAT-12 push, WAT-13 activation) and emit
    `WaterDirty` (ADR-013) — and `PathGrid.Invalidate(World.ChangedCells)`. Nothing may call `SetBlock` after this
    point in the tick: the change log is cleared at the end of the tick.
    **10b.** `Gravity.Tick` (M11-T9, GRV-02, ADR-079) runs between `AgentSystem.Tick` and `WaterGrid.EndTick`:
    buildings that no longer stand collapse (to a fixed point), then piles that no longer rest fall, at once
    (`specs/gravity.md`). It is the last step that may call `SetBlock`.
11. `Regions.RebuildIfDirty` — recompute reachability regions if topology changed this tick. Then
    `JobGiveUp.Tick` (JOB-12, ADR-058): reset give-up marks near this tick's walkability changes, drop stale ones,
    and every 50 ticks strike or recover sources by region reachability.
12. `Clock.Advance`.

Systems never call each other's `Tick`. Cross-system effects happen through shared state and `WorldActions`.

## Commands (ARCH-02)

Player intent enters the sim only as `ICommand`:

```csharp
public interface ICommand { void Apply(Simulation sim); }
```

MVP commands: `DesignateDig(box)`, `DesignateChop(area)`, `DesignateFarm(area)`, `CancelDesignation(box)`,
`PlaceBuilding(defId, origin, rotation)`, `Deconstruct(buildingId)`, `SetSpeed(n)` (view-only, not logged),
`SetSlice(y)` (view-only, not logged).

Commands validate on apply. Invalid commands are dropped and emit `SimEvent.CommandRejected(reason)`.
Commands are serialized (type tag + fields) into the command log.

## Events (ARCH-03)

The sim appends `SimEvent`s to `EventBus` during a tick. The view drains the bus after each tick. Events are for
the view only; sim systems never subscribe to events (they read state).

MVP events: `ChunkDirty(chunkIndex)`, `WaterDirty(chunkIndex)`, `AgentSpawned/Moved/Died(id)`,
`BuildingPlaced/Completed/Removed(id)`, `ItemPileChanged(cell)`, `CommandRejected(reason)`, `ColonyLost`.

## Queries (ARCH-04)

The view reads sim state through read-only accessors: `World.GetBlock(Int3)`, `World.GetChunkBlocks(ci)`
(`ReadOnlySpan<byte>`), `Water.GetLevel(Int3)`, `Agents.All` (`IReadOnlyList<Agent>`), `Buildings.All`,
`Storage.Totals`, `Clock`, `Weather`. The view may hold references to sim objects but must not mutate them.

## Threading (ARCH-05)

POC runs the sim on Godot's main thread inside `_Process` using a fixed-step accumulator (max 4 ticks per
frame to avoid spiral of death). Meshing also runs on the main thread, budgeted to 4 chunk rebuilds per frame
(queue the rest). Moving the sim to a worker thread is out of scope; the command/event design keeps that
possible later.

## Determinism and hashing (ARCH-06)

`Simulation.StateHash()` returns a 64-bit FNV-1a hash over: clock, RNG state, all block bytes, all water levels,
agents (sorted by id: position, needs, job id, carried stack), buildings (sorted by id), item piles (sorted by
cell index), storage contents, designations, farm tiles, plant states, weather state. Adding new state means
adding it to the hash. `GoldenHashTests` pins the hash for seed 1 at tick 6000.

## The action API (ARCH-07)

`WorldActions` is the only code that mutates blocks, item piles, carried stacks, and building progress on behalf
of an actor. Every method takes an `AgentId` and validates reach (actor must be standing adjacent to or on the
target cell, 26-neighborhood) and preconditions, then returns an `ActionResult` (`Ok`, `OutOfReach`,
`InvalidTarget`, `Blocked`, `InventoryFull`, …).

```csharp
ActionResult Dig(AgentId a, Int3 cell);                  // solid → air, spawns drop item pile
ActionResult Chop(AgentId a, PlantId tree);              // removes tree, spawns logs
ActionResult PlaceBlock(AgentId a, Int3 cell, BlockId b);
ActionResult PickUp(AgentId a, Int3 cell, ItemId item, int count);
ActionResult PickUpFromStorage(AgentId a, BuildingId b, ItemId item, int count);
ActionResult Drop(AgentId a, Int3 cell);
ActionResult DeliverTo(AgentId a, BuildingId b);         // storage or construction site
ActionResult Consume(AgentId a, BuildingId b, ItemId item); // eat / drink from storage
// M4-T5 adds: ActionResult Work(AgentId a, WorkTarget t)  — one tick of progress on construct / plant / harvest / pump
```

The player-avatar extension (out of scope) is an `Agent` driven by input instead of the job board, calling these
same methods.

## Data flow for a typical job

Designate dig → `DesignationSystem` posts `DigJob(cell)` → idle agent claims it (reservation on cell) →
`GoTo(adjacent standable)` → `Work(ticks = block hardness)` → `WorldActions.Dig` → block becomes Air, stone pile
appears, `ChunkDirty` emitted, `PathGrid` marks cells dirty, `Regions` flagged → `HaulSystem` posts haul job for
the pile next tick.
