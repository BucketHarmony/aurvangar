# Spec — Agents and jobs (JOB)

## Agents

- **JOB-01** `Agent`: `AgentId`, `Int3 cell`, `Int3 nextCell`, `int moveProgress`, `int hunger`, `int thirst`,
  `int health`, `ItemStack carried` (item + count, capacity `CarryCapacity = 10`, one item type at a time),
  `JobId currentJob`, `int stepIndex`, `int stepProgress`, `Int3[] path`, `int pathPos`, `AgentState`
  (`Idle`, `Working`, `Dead`).
- **JOB-02** Agents are processed in ascending `AgentId` order each tick. Dead agents are skipped but remain in
  the list (the view shows a marker) until save/load, which drops them.

## Job board

- **JOB-03** `JobBoard` holds open jobs in a `SortedDictionary<JobId, Job>`. Job ids are allocated monotonically.
- **JOB-04** A `Job` has: `JobKind`, `Priority` (int, higher first), target (cell or building), `Reservations`
  (cells, item counts, storage slots) taken on claim and released on completion or failure, `ClaimedBy`.
- **JOB-05** Job kinds and priorities:

| Kind | Priority | Posted by | Steps |
|---|---|---|---|
| Drink | 100 | self (need) | GoTo(storage with water) → Consume |
| Eat | 90 | self (need) | GoTo(storage with food) → Consume |
| Flee | 200 | self (deep water) | GoTo(nearest dry) |
| Deliver | 50 | BuildingSystem (site needs material) | GoTo(source storage/pile) → PickUp → GoTo(site) → DeliverTo |
| Construct | 45 | BuildingSystem (site has all materials) | GoTo(site adjacent) → Work(build ticks) |
| OperatePump | 40 | BuildingSystem (pump has output capacity) | GoTo(pump) → Work(produce cycle) repeat until relieved |
| Harvest | 35 | PlantSystem (ripe crop / bush, storage below target) | GoTo → Work(20) → carries produce |
| Plant | 30 | FarmSystem (empty farm tile) | GoTo → Work(30) |
| Dig | 25 | DesignationSystem | GoTo(adjacent) → Work(hardness) → Dig |
| Chop | 25 | DesignationSystem | GoTo(adjacent) → Work(80) → Chop |
| Haul | 20 | HaulSystem (loose pile / carried produce) | GoTo(pile) → PickUp → GoTo(storage) → DeliverTo |
| Deconstruct | 25 | Deconstruct command | GoTo → Work(half build ticks) → refund pile |
| Build (M8-T2) | 25 | BlockBuildSystem (CON-12) | GoTo(storage) → PickUpFromStorage → (GoTo(stand) → Work(buildTicks) → Place) per cell |

- **JOB-06** Job selection (idle agent, every tick while idle, max 1 attempt per agent per 5 ticks): among open
  unclaimed jobs, filter by region reachability (PTH-13) and preconditions (e.g. a storage has the item),
  then choose max `Priority`, then min Manhattan distance from agent to target, then min `JobId`.
- **JOB-07** Need jobs (Drink, Eat, Flee) are generated per-agent when the need threshold is crossed and are
  claimed immediately by that agent, preempting any current non-need job. A preempted job is released back to
  the board with its reservations released and any carried item dropped on the agent's cell (becomes a pile).
- **JOB-08** Every step that touches the world calls `WorldActions`. If an action returns non-Ok, the job fails:
  reservations released, job returned to the board with a `retryAfterTick = now + 50` cooldown. After 5 failures
  a job is cancelled and its designation is marked `Unreachable` (view shows it in red); a recurring job's source
  gets a JOB-12 strike instead.
- **JOB-09** Dig designations below the agent: an agent must not dig the cell it stands on or the cell directly
  below itself. Dig jobs choose an adjacent standable cell that stays standable after the dig when possible.
  A stand cell the dig would cut off from the Great Hall is never used, and no dig is taken or finished while it
  would cut another dwarf off (DSG-09).

## Haul logic

- **JOB-10** A loose item pile (from dig, chop, harvest, refund, dropped carry) posts one Haul job per pile when
  some storage accepts the item with free capacity. Storage choice: nearest by Manhattan with capacity, tie
  by building id. Capacity is reserved on claim.
- **JOB-11** Harvest jobs end with the agent carrying produce; they chain into a Haul to storage within the same
  job (steps appended) rather than dropping. With no storage room for the produce, the appended step is a Drop (ADR-047).

## Give-up marks (M7-T5, ADR-058)

- **JOB-12** Recurring jobs that never succeed. The sources are a construction site's Deliver jobs, a pump's
  OperatePump job, a pump's buffer Haul, and a loose pile's Haul. A source earns a strike when one of its jobs is
  cancelled at its fifth failure (JOB-08), or when a check every 50 ticks finds its open job out of every living
  agent's region (PTH-13). Such a job is never claimed, so it never fails. At 3 strikes the source is given up: its
  open jobs are withdrawn, it posts none, and the HUD shows "Unreachable: <names>" (VIEW-15). A mark is removed when:
  - a job of the source completes;
  - a walkability change (a block, plant, construction site or deep-water change) lands within 8 cells (Chebyshev) of
    the mark's cell (site entrance, pump stand cell, pump entrance, pile cell);
  - a storage building is completed (this clears every mark);
  - the source is gone;
  - for a mark struck by the region check, its cell is back in a living agent's region (checked every 50 ticks).

  Marks are saved and hashed.

## Construction flow

See `buildings.md` BLD-05..BLD-09.

## Acceptance scenarios

1. One agent, one dig designation on a stone cell adjacent to its standable area: after enough ticks the cell is
   Air, one stone pile appears, then the stone ends up in hub storage.
2. Two agents, one job: exactly one claims it; the other stays idle.
3. Priorities: with a Haul and a Construct open, an idle agent takes Construct.
4. Preemption: an agent hauling logs becomes thirsty → drops logs, drinks, logs are later hauled by someone.
5. Unreachable: a dig designation sealed inside rock is never claimed (region filter), no A* is run for it
   (counter `Pathfinder.Searches` unchanged).
6. Failure: a job whose target becomes invalid mid-way fails cleanly and is retried after the cooldown.
