# Spec — Free-form block construction (CON)

G3 play feedback: "I want to build great constructs, I want to plan monuments." The player paints structure block by
block; dwarves fetch the material and build it. Prefab buildings (`buildings.md`) stay for functional buildings
(Great Hall, Warehouse, Pump, Levee). ADR-061 amends ADR-001.

Terms:
- **Construction block**: a block type with a `cost` in `data/blocks.json` (CON-01). A cell holding one is a
  **built block**. Every other solid block (terrain, Bedrock, BuildingSolid) is **ground**.
- **Plan entry**: a cell the player wants filled with a given construction block. It is `Planned` (dwarves ignore
  it) or `Released` (dwarves build it). Entries exist only for cells that are not built yet.
- **Build job**: one trip. A dwarf fetches material from storage and places one or more blocks.

The world, water, path and region code needs no new rules for built blocks. They are solid blocks, so they are
floors (PTH-01), hold water (WAT-12) and count as ground for buildings (BLD-02).

## Block types (CON-01..03)

- **CON-01** Three construction block types, added to `data/blocks.json` and to the `BlockId` enum with the same
  names (M8-T2):

  | Id | Name (enum) | `label` (player text) | `cost` | `buildTicks` | `hardness` (dig ticks) | Palette colour |
  |---|---|---|---|---|---|---|
  | 8 | `Masonry` | Stone wall | `{ "stone": 1 }` | 20 | 40 | `#a39e94` |
  | 9 | `Planks` | Wood planks | `{ "log": 1 }` | 15 | 20 | `#b98a52` |
  | 10 | `PolishedStone` | Polished stone | `{ "stone": 2 }` | 40 | 60 | `#d8d2c4` |

  All three are `solid: true`, `diggable: true`, `drop: null` (CON-17 refunds the cost instead). `BlockDef` gets
  three optional fields: `Label` (string, default null), `Cost` (`Dictionary<string,int>`, default null) and
  `BuildTicks` (int, default 0). A block is a construction block if and only if `Cost` is not null.
  `ContentDb.IsConstruction(BlockId)` answers this from a `bool[256]` table, the same way `SolidTable` works.
  Palette keys are the enum names (`palette.blocks.Masonry`, ...).
- **CON-02** `ContentDb` validation. Each error throws naming the file and the block:
  - Every `blocks.json` id has a `BlockId` value, and every value has an entry with the same name. Today only the
    second direction is checked.
  - A construction block has:
    - exactly one cost item, which exists in `items.json`, with a count in 1..10 (`Agent.CarryCapacity`; a pile and
      a carried stack hold one item type);
    - `buildTicks >= 1`, `solid` and `diggable` true, `drop` null, and a non-empty `label`.
  - A non-construction block has no `buildTicks` (0).
  - Every block except Air has a `palette.blocks` colour.

  `ContentDbTests` asserts `Blocks.Count == 8`. M8-T2 changes that to 11 (the block table changed; record it in
  PROGRESS.md).
- **CON-03** A built block behaves like terrain everywhere outside this spec:
  - It is solid for water and paths.
  - Buildings may stand on it (BLD-02).
  - It is not farmable (ECO-11 takes only Grass/Dirt).
  - `WorldActions.Dig` digs it (CON-17).

  Its state is the block byte, so the world hash and save need nothing new for built blocks.

## Plan entries (CON-04..06)

- **CON-04** `BlockPlans` (`Simulation.Plans`, new `Blocks/` folder in `Aurvangar.Sim`, ADR-062) holds entries in a
  `SortedDictionary<int cellIndex, PlanEntry>`. A `PlanEntry` is `(BlockId Block, PlanState State)`, where
  `PlanState : byte { Planned = 1, Released = 2 }`.
  - Placing the block removes the entry (the `Place` step does it, CON-13).
  - At ARCH-01 step 8, any entry whose cell is solid is removed, whatever filled it (a future avatar, a building).
  - Hashed after the give-up marks: count, then `index, block, state` in index order. The section is added only
    when there is at least one entry, so a game without plans hashes as before and the seed-1 goldens do not move.
  - Saved in a new `BlockPlans` section after `GiveUps`, in index order. `SaveGame.FormatVersion` goes 5 -> 6 in
    M8-T2. The state byte is saved from the start, so M8-T4 needs no second bump.
- **CON-05** Status of an entry (derived, never stored). `BlockPlans.StatusOf(sim, cell)` checks these in order;
  the first one that applies is the answer:
  1. `Planned`: the entry is not released.
  2. `InJob`: a Build job holds the cell (CON-12).
  3. `GivenUp`: its JOB-12 mark (CON-15) is given up.
  4. `BelowFirst`: the cell directly below has an entry. This gives bottom-up order.
  5. `NoSupport`: placement support fails (CON-09).
  6. `CourseBelow` (M9-T2, ADR-068): a Build job holds a cell one course down (y - 1) within
     `BlockPlans.CourseRadius` (8) horizontally (Chebyshev). A structure then rises course by course, and a whole
     course turns `Ready` together, so it goes out in full batches. Only held cells count, so a stuck entry below
     never holds anything up. The re-check and the run of a job ignore the job's own cells.
  7. `Occupied`: one of these is in the way:
     - a living agent holds the cell, or the cell below it (the headroom; `Agents.AnyHolds`);
     - a pile is in the cell;
     - a plant occupies the cell.
  8. `NoAccess`: no build stand cell (CON-11, before the strand filter) is in a living agent's region.
  9. `WouldStrand`: every stand cell is removed by the strand filter, or placing the block would cut another
     living dwarf off from the hall (CON-14).
  10. `NoMaterial`: no complete storage accepting the cost item holds at least one unpromised cost unit, with its
     `GoToBuilding` goals in the region of one of the cell's stand cells.
  11. `Ready`.

  The build poster uses exactly this function (`Ready` means a job may be posted). Checks 1 to 5 and 7 are O(1);
  check 6 scans the held cells one course down (built once per scan). Checks 8 and 9 cost a region lookup and a cached what-if flood. The view reads statuses (VIEW-22). It must not call
  `StatusOf` for every entry every frame; M8-T5 decides how often.
- **CON-06** Material totals. `BlockPlans.Needed(PlanState? state)` returns, for each item, the sum of cost over
  entries in that state (or over all entries), in ascending item id order. The HUD compares it with
  `Storage.Totals` (VIEW-23). There is no reservation or cache; the value is a sum over entries. Keep it O(entries):
  the view calls it at most every 10 frames.

## Commands and shapes (CON-07..08)

- **CON-07** Commands. All four are logged commands with `CommandCodec` entries; the tag is the type name, and
  `BuildShape` and `BlockId` are written as bytes.
  - `DesignateBuild(BuildShape Shape, Int3 A, Int3 B, int Height, BlockId Block, bool Plan)`.
    `BuildShape : byte { Single, Line, Wall, Floor, HollowBox, Stair }`. The cells come from
    `BuildShapes.Cells(shape, A, B, height)`, a pure static function in the sim that the view also uses. All shapes
    are axis-aligned. `A` is the anchor and `A.Y` is the base level; `B.Y` is ignored.

    | Shape | Cells |
    |---|---|
    | `Single` | `A` |
    | `Line` | If `|B.X-A.X| >= |B.Z-A.Z|`: `(x, A.Y, A.Z)` for x from A.X to B.X. Otherwise `(A.X, A.Y, z)` for z from A.Z to B.Z |
    | `Wall` | the `Line`, on levels `A.Y .. A.Y+Height-1` |
    | `Floor` | the rectangle `x in [min,max], z in [min,max]` at `A.Y` |
    | `HollowBox` | the rectangle's perimeter ring (x or z on an edge) on levels `A.Y .. A.Y+Height-1`. Top and bottom are open; a roof is a separate `Floor`. With width or depth 1 it is a `Wall` |
    | `Stair` | a flight along the `Line` direction: step `i` (i = 0..n-1) is the i-th `Line` cell raised to `A.Y + i`. Cells under the steps are not included |

    - `Height` counts only for `Wall` and `HollowBox`; the other shapes use 1.
    - Rejections (`CommandRejected(reason)`, checked in this order):
      - `BadHeight`: Height is outside 1..32.
      - `NotBuildable`: Block is not a construction block.
      - `TooLarge`: the shape has more than 4096 cells.
      - `OutOfWorld`: no cell is in the world.
    - Otherwise each cell is validated by CON-08. Valid cells get an entry in state `Planned` if `Plan` is true,
      else `Released`. Invalid cells are skipped. If no cell is valid, the command is rejected with `NothingToBuild`.
    - A cell that already has an entry takes the new block and state (repainting). If the block changes, a Build
      job holding the cell is cancelled (DSG-06 semantics).
    - Cells are listed in ascending `(y, index)` order.

    A door is a gap. The player either paints the walls around it, or paints a `HollowBox` and cancels the door
    cells before releasing.
  - `ReleasePlan(Int3 A, Int3 B)` (M8-T4): every `Planned` entry in the box becomes `Released`. With none it is
    rejected with `NothingToRelease`. To release the whole plan, use a box that covers the world.
    The box is inclusive with its corners in any order; the command walks the entries, not the box's cells, so a
    world-sized box is cheap. Released entries are not re-validated; they show their CON-05 status (ADR-064).
  - `CancelDesignation(A, B)` (DSG-06) also removes every entry in the box, in either state, and cancels the Build
    jobs that hold them. A claimed job is released: the dwarf drops what it carries (JOB-07), and the pile is hauled
    back (JOB-10). Blocks already placed stay. Nothing unplaced was spent, so nothing is refunded.
  - `DesignateDeconstructBlocks(Int3 A, Int3 B)` (M8-T3): see CON-18.
- **CON-08** Plan validity of one cell, `BlockPlans.CanPlan(sim, cell, pending)` -> `PlanResult`. The checks run in
  this order and the first failure is the reason:
  1. `OutOfWorld`: not in bounds, or y = 0.
  2. `Solid`: the cell is solid now. Water in the cell is fine.
  3. `Building`: a footprint cell of any building in any state, or a building's entrance or stand cell (BLD-03,
     BLD-04).
  4. `Plant`: a tree trunk, bush or crop occupies it.
  5. `Farm`: the cell below is a farm tile. Crops grow there. ECO-11 likewise skips a tile whose cell above has an
     entry.
  6. `Unsupported`: plan support fails (CON-09).
  7. `Ok`.

  `pending` is the command's own cell set, used for plan support. Agents and piles do not invalidate a cell; the
  build waits for them (CON-05 `Occupied`).

  BLD-02 gets a new failure, `PlannedBlocks`: a footprint cell, or the entrance or stand cell, has an entry. It is
  checked right after `Overlaps` (M8-T2).

## Support (CON-09..10)

- **CON-09** No floating blocks.
  - **Grounded**: a built block is grounded if a path of face steps leads from it to a ground block, going only
    down (-y) or sideways (+-x, +-z) and passing only through built blocks. Nothing hangs from above.
  - **Invariant**: every built block in the world is grounded. The M8-T6 test checks it: "no floating block".
  - **Placement support**: the cell's down neighbor or one of its 4 horizontal neighbors is solid. By the invariant,
    such a neighbor is ground or grounded, so the check is local. `WorldActions.PlaceBlock` checks it (new result
    `ActionResult.Unsupported`), and so does status check 5.
  - **Plan support** (CON-08 `Unsupported`): the same rule, with "solid" widened to "solid, or has an entry in
    either state, or is in `pending`". It is applied transitively: the cell is plan-supported if a down/sideways path
    through such cells reaches a solid cell. One flood per command, capped at 16384 visited cells (over the cap
    counts as unsupported).
  - Natural terrain is always ground, even if digging left it floating. Terrain never collapses.
- **CON-10** A removal must not unground a built block. Removing a solid block X is allowed only if every built block
  among X's up neighbor and 4 horizontal neighbors stays grounded without X. This covers a dig of any block, and a
  complete building's deconstruction, whose footprint BuildingSolid is ground.
  - Test: `Support.Depends(sim, X)`. From each such neighbor, run a BFS over built blocks along down and sideways
    steps, never entering X. It succeeds on reaching a ground block. The cap is 4096 visited cells per neighbor;
    over the cap counts as "depends" (conservative: it waits).
  - Dig jobs (M8-T3): `DesignationSystem` does not post a dig whose block others depend on. `JobRunner.Select`
    skips it, and the Dig step stands down with the JOB-08 cooldown and no failure, like ADR-037.
    - Such a mark waits and never turns `DigUnreachable` for this reason.
    - DSG-03 exposure and the DSG-04 top-down bias then take a structure down top-first.
    - A sideways span comes down from its free end.
  - Work start also stands down for such a dig, as for the strand rule (M8-T3, ADR-063).
  - `WorldActions.Dig` returns `Blocked` for such a block, so the API keeps the invariant.
  - BLD-09: `Deconstruct` of a complete `setsBlocks` building with any footprint cell that others depend on is
    rejected with `SupportsBlocks`. It is checked after `BuildingOnTop`. A block placed against the building after
    the command was accepted holds the teardown back: the last Deconstruct tick is `Blocked` in `WorldActions` and
    the job stands down (no failure) until the block is gone (ADR-063).
  - The search is depth-first and tries the down step first, so a column or wall answers in about its height.

## Build jobs (CON-11..15)

- **CON-11** Build stand cells, new `GoalMode.Build` in `JobGoals`:
  - A stand cell is a walkable cell `c` in reach of the target `T` (26-neighborhood, ARCH-07) with `c != T` and
    `c != T + down`. The block would take either the stand cell or its headroom.
  - Cells rejected by the strand filter (CON-14) are dropped.
  - Prefer stand cells that have no entry, whose floor has no Dig mark, and that lie in a living agent's region
    (M8-T6, ADR-066: a free cell no dwarf can reach, such as the top of an unfinished wall, must not hide the
    reachable cells). If there are none, use all stand cells. This mirrors `DigStandCells`.
  - Order is ascending cell index.

  A dwarf may stand on built blocks. A wall taller than 2 is built from stand cells on lower courses, or from a
  `Stair`. There is no scaffolding: the plan must give dwarves a way up (ADR-061).
- **CON-12** Posting: `BlockBuildSystem.Tick`, at ARCH-01 step 8, after the dig and chop postings.
  - **Order**: released entries in ascending `(y, index)` order that are `Ready` and not held by a job.
  - **Batch**:
    - The seed is the first such entry. An entry whose JOB-12 mark is given up is never a seed.
    - Up to `floor(10 / cost) - 1` more entries join. Each must be `Ready`, unheld and the same block type, within
      Chebyshev distance 4 of a batch member, with a stand cell in a region shared with a stand cell of the seed.
      Members are taken in order, the seed first; around each, the box is scanned in ascending `(dy, dz, dx)` order
      and each new entry joins at the end (M9-T2, ADR-068: a batch follows a wall course instead of stopping at a box
      around the seed). The members after the seed are then stably sorted by y, so lower cells are built first.
    - One Build job is posted per batch, and it holds its cells until they are placed, skipped or cancelled.
  - **Limits**: at most 8 new Build jobs per tick, and at most 32 unclaimed Build jobs at a time.
  - **Re-check**: each tick, an unclaimed Build job with a cell that is no longer `Ready` (ignoring its own hold and
    its own cells in the course check, and ignoring agents in the `Occupied` check, M9-T2: a dwarf walking over a wall
    top must not break up the batches of the builders standing on it) is cancelled, and its cells are batched again later. Otherwise its placed cells leave it and it is re-planned
    against the current stock (source, count), keeping its JOB-08 failure count (ADR-062).
  - **Job**: `JobKind.Build`, priority 25 (the same as Dig and Chop; JOB-05 row added in M8-T2). The target is the
    seed cell. Steps:
    `GoTo(source, Building) -> PickUpFromStorage(source, item, n) -> for each cell k: GoTo(cell_k, Build) -> Work(cell_k, buildTicks) -> Place(cell_k, block)`,
    with `n = cells x cost`.
  - **Source**: chosen by the ADR-041 rule:
    - the nearest complete storage that accepts the item and has `n` unpromised;
    - else the storage with the most, with the batch trimmed to the whole cells it covers;
    - else nothing is posted (`NoMaterial`).

    Loose piles are not a source (ADR-041). The job reserves the source stock (BLD-10).
  - New `StepKind.Place` calls `WorldActions.PlaceBlock` with the step's cell and block. The block goes in
    `JobStep.Target`.
- **CON-13** Running a Build job:
  - **Skipping**: when the GoTo of cell k starts, the runner re-checks the cell. It must still have a released entry
    of the job's block, and its status, ignoring its own hold, must be `Ready`, `Occupied` or `NoMaterial`. If not,
    the cell's three steps are skipped and its hold released. That is not a failure.
  - **Place**: `PlaceBlock` for a construction block checks, in order:
    1. actor, then reach (as today);
    2. the cell is in bounds and not solid, and the block is valid (else `InvalidTarget`);
    3. the cell is in no building footprint and is no entrance or stand cell (else `Blocked`);
    4. no agent in the cell or the cell below, no plant, no pile (else `Blocked`);
    5. placement support (else `Unsupported`);
    6. the carried stack: empty gives `InventoryEmpty`, another item gives `WrongItem`, fewer than the cost gives
       `NotEnoughItems`.

    Then it removes the cost from the carried stack and sets the block. The `Place` step then removes the entry.
    Natural blocks keep the ADR-027 behaviour (free, no support check; used only by tests).
  - **Waiting**: `Blocked` by an agent waits up to `JobRunner.DigDeferLimit` (200) ticks, as in DSG-08, and then
    fails the job. Other non-Ok results fail the job (JOB-08).
  - **Re-goal**: a GoTo(Build) whose move fails because its goal stand cell stopped being walkable (another block
    went into it) restarts its step, which re-checks the cell and picks fresh stand cells; it is not a failure
    (ADR-062).
  - **Surplus**: material left over from skipped cells stays carried. The idle agent drops it when the job ends
    (existing behaviour), and JOB-10 hauls it back.
  - **Step-aside**: an idle dwarf standing in a released entry's cell, or in the cell below one, steps aside (ADR-062). This
    extends ADR-029's `StepAside`.
- **CON-14** No walling a dwarf in (G3: "reuse the strand rule").
  - **What placing at P removes**:
    - P as a walkable cell;
    - P + down, which loses its headroom;
    - moves that need headroom through P (PTH-05: P = a+up+up or b+up+up);
    - diagonal moves with P as an intermediate (PTH-06).

    It adds P + up as a standable cell.
  - **What-if view**: a new `PlaceTrialCells : IMoveCells` sees P as solid. P's water is treated as gone, and water
    pushed into neighbours is ignored, the same way ADR-037 treats the dug cell as dry.
  - **`PlaceTrial`** mirrors `DigTrial`:
    - `MaySplit(P)` is the local test. It floods from the move neighbours of P and P + down on the what-if view,
      within `LocalBudget` (512). If they all meet again, the placement cuts nothing.
    - `MayCut(P, anchors, key, cell)` computes the exact cut set.
    - Both are cached per cell until `WalkabilityVersion` or `DeepVersion` moves, or the hall changes.
  - **`PlaceStrand`** has `Strands(sim, P, stand)` and `StrandsOthers(sim, P, except)`, like `DigStrand`, with the
    same Great Hall anchors. Anchors equal to P or P + down are left out of the what-if anchor set.
  - **Where it applies**, as the dig rule does:
    - stand cells (CON-11);
    - the last filter in `JobRunner.Select`;
    - Work start and the `Place` step (stand down with the JOB-08 cooldown, no failure).
  - **Step-out**: an idle dwarf in a pocket that the placement of a released entry would cut off walks towards the
    hall (`PlaceStrand.StepOut`; it scans entries, not jobs, because such an entry is `WouldStrand` and has no job;
    ADR-062).
  - With no complete hall, the rule is off.
  - An entry that waits for this has status `WouldStrand` and is never given up for it.
  - Closing a room with no dwarf inside is allowed. The rule protects dwarves, not piles or buildings; JOB-12 covers
    those.
- **CON-15** Give-up marks (JOB-12, ADR-058):
  - New `GiveUpSource.Build`, keyed by the seed cell's index. The mark cell is the seed cell.
  - `JobGiveUp.SourceOf` gets the Build case, so fifth-failure cancels and the 50-tick region check strike it.
  - `BlockBuildSystem` checks `sim.GiveUps` before seeding.
  - A job that places at least one block completes, which clears the mark. The other JOB-12 resets apply unchanged.
    An entry that is gone drops its mark.
  - `TopBarModel.GiveUpText` names the source by the block label ("Unreachable: Stone wall x3").
  - Build marks are not dropped by the region recovery of `JobGiveUp.StrikeUnreachable`; they clear by the other
    resets or when the entry is gone (ADR-062).

## Water (CON-16)

- **CON-16** A built block is solid like BuildingSolid:
  - Placing it in a wet cell pushes the water out (WAT-12). The volume is conserved (WAT-11, with overflow counted
    as `Evaporated`).
  - A wall of built blocks holds water exactly like a levee. The CA treats every solid alike, with no pressure
    (WAT-08).
  - A build cell may be under water, as long as its builder stands on a walkable (wadeable) cell in reach.
  - Digging a built block lets water in like any dig (WAT-13).

## Deconstruction (CON-17..18)

- **CON-17** Digging a built block takes its `hardness` in ticks. It drops the block's whole cost as one pile on the
  dug cell: 1 stone for Masonry, 1 log for Planks, 2 stone for PolishedStone. The refund is 100%, unlike BLD-09's
  50%: blocks are cheap, and redesigning a monument should not be punished. The cost is one item type of at most 10
  (CON-02), so it always fits one pile. Natural drops are unchanged.
- **CON-18** `DesignateDeconstructBlocks(A, B)` sets Dig marks (DSG-01) on built blocks in the box, and only on them.
  - With none (including a box wholly outside the world), it is rejected with `NothingToDeconstruct`.
  - The rest of DSG applies: exposure (DSG-03), top-down bias (DSG-04), the strand rule (DSG-09) and the CON-10
    support wait.
  - `DesignateDig` also marks built blocks (they are diggable). This command exists so a drag over a monument does
    not dig the ground under it.

## Budget

- **CON-P1** SIM-P1 holds during construction: the full `Tick()` median is at most 8 ms on seed 1 with
  `MonumentScript` mid-build (M8-T6 perf test).

## Task map

| Task | Implements |
|---|---|
| M8-T2 | CON-01..05 (with the `Planned` state stored; the poster ignores it), CON-07 `DesignateBuild` and the `CancelDesignation` extension, CON-08 (with BLD-02 `PlannedBlocks`, ECO-11 skip), CON-09, CON-11..16, save v6 and hash |
| M8-T3 | CON-10 (dig wait, `Dig` Blocked, BLD-09 `SupportsBlocks`), CON-17, CON-18 |
| M8-T4 | CON-06, CON-07 `ReleasePlan`, `Plan = true` entries and the `Planned` status end to end |
| M8-T5 | VIEW-21..23 (`view-ui.md`); built blocks mesh with CON-01 palette colours |
| M8-T6 | `MonumentScript` and CON-P1; the invariants of CON-09 and CON-14 over a whole session |

## Acceptance scenarios

1. A 6x3 Masonry wall with stone in the hub is built bottom-up: in each column the lower block is placed before the
   upper one, and the stone taken from storage equals the blocks placed.
2. Floating cells, cells above pending entries and cells with no stock post no job, with statuses `NoSupport`,
   `BelowFirst` and `NoMaterial`.
3. A Masonry wall across a channel lowers the downstream level like the levee scenario (`buildings.md` 3). Placing
   into a wet cell conserves volume.
4. A closed ring is not finished while a dwarf works inside; an idle dwarf steps out, then the ring completes. No
   dwarf ever leaves the hall's region.
5. A 5-high wall with a stair against it completes, with builders standing on built blocks.
6. Cancelling mid-build removes the entries and jobs, keeps placed blocks, and loses no stone.
7. Taking a tower down removes it top-first. No built block is ever ungrounded, and a dig under a wall waits.
8. Plan entries are not built until released. The material totals match the entries.
9. Save and load mid-build continue identically (SAV-03).
