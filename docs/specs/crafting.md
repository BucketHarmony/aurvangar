# Spec — Economy and crafting (CRF)

M11 (G6 answers, ADR-082). The human picked **economy and crafting** as the missing gameplay element: workshops that
refine materials, and trade wagons. This spec adds two refined items, two workshops with recipes and orders, refined
costs for three construction blocks, and a trade wagon that visits the Great Hall on a schedule.

Everything here is integers, data-driven and deterministic. Nothing uses `Rng`. The trader's schedule is a pure
function of the tick, like the weather (ECO-17).

Out of scope (ADR-082): money and prices, recipes with more than one input or output item, worker assignment or
skills, fuel or power, quality, trader factions or reputation, selling arbitrary items, a trader that travels across
the map, and more than one trader definition.

## Items (CRF-01)

- **CRF-01** Two refined items are appended to `data/items.json`, after `water`, so existing item ids do not move:

  | id | Name | ItemId | Made by | Palette colour |
  |---|---|---|---|---|
  | `planks` | Planks | 6 | Sawmill (CRF-05) | `#c79a5e` |
  | `cutstone` | Cut stone | 7 | Stonecutter (CRF-05) | `#bdb6a8` |

  Neither is food or drink. The hub and the warehouse accept both (`storage.accepts`). The starting wagon accepts both
  and its `startStock` gains **20 planks and 20 cut stone** (BLD-15), so refined blocks can be tried on day 1 and the
  screenshot scripts that paint them keep working. Anything more has to be made or bought.

## Refined blocks (CRF-02)

- **CRF-02** Three construction blocks move to refined costs in `data/blocks.json` (CON-01). The rough blocks keep
  their raw costs:

  | Id | Block (label) | Cost before | Cost after | Kind |
  |---|---|---|---|---|
  | 8 | Masonry (Stone wall) | 1 stone | 1 stone | rough |
  | 9 | Planks (Wood planks) | 1 log | **1 planks** | refined |
  | 10 | PolishedStone (Polished stone) | 2 stone | **2 cutstone** | refined |
  | 11 | Rubble (Rough stone) | 1 stone | 1 stone | rough |
  | 12 | Beam (Wood beam) | 2 log | 2 log | rough (a hewn log) |
  | 13 | Slate (Slate tiles) | 3 stone | **3 cutstone** | refined |

  - Counts do not change, so build batches, CON-20 shaped costs and the view's totals behave as before. Only the item
    changes. A Sawmill turns 1 log into 2 planks, so Wood planks cost half a log once refined. Cut stone is 1:1, so
    Polished stone and Slate cost the same stone as before plus the stonecutter's work.
  - CON-17 refunds the refined item: digging a Wood planks block drops 1 planks.
  - Build jobs fetch the refined item from storage like any cost item (CON-12). No construction code changes; the cost
    item comes from data.

## Workshops (CRF-03..05)

- **CRF-03** A workshop is a building with an optional `workshop` block in `data/buildings.json`:

  ```json
  "workshop": {
    "outputBuffer": 20,              // most items the workshop holds (all outputs together)
    "haulAt": 10,                    // an Unload job is posted at this many unpromised items (CRF-12)
    "recipes": [
      { "id": "planks", "name": "Saw planks", "input": { "log": 1 }, "output": { "planks": 2 }, "workTicks": 40 }
    ]
  }
  ```

  `BuildingDef` gets `Workshop` (`WorkshopDef?`, default null). A workshop's output sits in `Building.Stored`, like a
  pump's buffer (BLD-13): it is not a storage (`storage` is null), it is not in `Storage.Totals` and nothing is hauled
  into it. The recipes live inside their building, so `ContentDb.Load` keeps its signature (as the shapes did,
  ADR-080).
- **CRF-04** Recipes. Each recipe has an `id`, a player-facing `name`, exactly **one input item** with its count, exactly
  **one output item** with its count, and `workTicks`, the work per cycle. `ContentDb` validation throws, naming the
  file, the building and the recipe, when:
  - a workshop has workers other than 1, no entrance, a storage, a producer, `waterEdge` placement or no recipes;
  - `outputBuffer` < 1, or `haulAt` is not in 1..`outputBuffer`;
  - a recipe has an input or output that is not exactly one known item;
  - an input count is not in 1..`CarryCapacity` (10), or an output count is not in 1..`outputBuffer`;
  - `workTicks` < 1;
  - a recipe id is used twice (ids are unique over all workshops).

  `ContentDb.Recipe(id)` returns a recipe with its workshop. Recipes are indexed by their position in the workshop's
  list (the "recipe index").
- **CRF-05** Two workshops (data):

  | id | Name | Footprint | Entrance | Cost | Build ticks | Recipe |
  |---|---|---|---|---|---|---|
  | `sawmill` | Sawmill | 2×2×2 | `[0,0,-1]` | 16 log | 240 | `planks`: 1 log → 2 planks, 40 ticks |
  | `stonecutter` | Stonecutter | 2×2×2 | `[0,0,-1]` | 8 log, 8 stone | 240 | `cutstone`: 1 stone → 1 cut stone, 50 ticks |

  Both are `ground`, `setsBlocks`, 1 worker, `outputBuffer` 20, `haulAt` 10. They are placed and built like any
  prefab (BLD-01..09), fall like any building (GRV) and are torn down with BLD-09 (their output is dropped with the
  refund, as stored items are).

## Orders (CRF-06..09)

- **CRF-06** Each workshop has at most one **order** per recipe: `(recipe index, mode, count, done)`.
  - `mode` is **Make** ("make N": craft until `done` reaches `count`, then the order is removed) or **Keep** ("keep at
    least N in stock": craft while the colony stock of the output is below `count`; the order stays until changed).
  - `count` is 1..999 items of the output (not cycles). `done` counts outputs made for a Make order; it is 0 for Keep.
  - Orders are kept in recipe-index order, not in the order they were set. A workshop with no orders stands idle.
  - Orders can be set on a workshop in any state. They run only while it is complete.
- **CRF-07** Command `SetWorkshopOrder(BuildingId Workshop, int Recipe, OrderMode Mode, int Count)`, logged, with a
  `CommandCodec` entry. It sets or replaces the order for that recipe; a replaced order's `done` goes back to 0.
  `Count = 0` removes it. Rejections, checked in this order, each emit `CommandRejected`:
  - `NoSuchBuilding`, when no building has that id;
  - `NotAWorkshop`, when the building has no `workshop` block;
  - `BadRecipe`, when the index is not in its recipe list;
  - `BadMode`, when the mode byte is undefined;
  - `BadCount`, when the count is outside 0..999;
  - `NothingToRemove`, when the count is 0 and there is no order to remove.

  A claimed Craft job is never cut short by an order change. It finishes its cycles, and the outputs go to storage.
- **CRF-08** Colony stock of an item, `Economy.Stock(sim, item)`: the sum of
  - `Stored` over complete storage buildings (the hall, warehouses, the starting wagon and a trader's bought goods,
    CRF-18);
  - every workshop's output held in `Stored`;
  - what living agents carry.

  Loose piles are not counted. The sum is taken when it is needed, and not cached.
- **CRF-09** The **active order** of a complete workshop is its first order, in recipe order, that
  - still wants output: Make with `done < count`, or Keep with `Stock(output) < count`;
  - has input: some complete storage in the region of the workshop's entrance holds at least one cycle of input,
    unreserved (stock minus StorageOut reservations).

  A job for the active order runs `k` cycles, the smallest of:
  - the cycles still wanted: `ceil((count - done) / out)` for Make, or `ceil((count - Stock(output)) / out)` for Keep;
  - `CarryCapacity / in` (whole cycles a dwarf can carry);
  - `(outputBuffer - Stored total) / out` (room for the outputs);
  - `unreserved input at the source / in`.

  If `k` is 0, no job is posted. The source storage is the nearest complete storage, by Manhattan distance from the
  entrance and then by id, that has one cycle of input unreserved (as BLD-06 picks a source).

## Craft and unload jobs (CRF-10..14)

- **CRF-10** Posting, `Workshops.Tick` at ARCH-01 step 7, after `Pumps.Tick`. Each complete workshop keeps **at most
  one Craft job**, since it has 1 worker:
  - **Kind** `Craft`, priority **30**: above Dig, Chop and Build (25), and below the pump (40), Harvest (35) and
    construction (45, 50).
  - **Steps:** `GoToBuilding(source) → PickUpFromStorage(source, input, k × in) → GoTo(entrance, Exact)`, then per cycle
    `WorkOn(workshop, workTicks) → Craft(workshop, recipe index)`.
  - **Reservation:** StorageOut `k × in` at the source.
  - **Target:** the workshop's entrance.

  An unclaimed Craft job that no longer matches the plan is withdrawn and a new one is posted in the same tick. That
  happens when the order, the source, `k`, or the workshop state changed. A workshop that is not complete (torn down,
  collapsing, gone) cancels its Craft jobs, claimed ones too: the crafter drops its input, which is hauled back
  (CRF-14, ADR-083).
- **CRF-11** `WorldActions.Craft(agent, workshop, recipe)` is a new StepKind, `Craft`.
  - **Checks**, in order, returning the first failure (JOB-08):
    - the workshop exists and is complete;
    - the agent is on its entrance (as for the pump, BLD-13);
    - the agent carries the recipe's input, at least `in` of it;
    - the output fits in `outputBuffer`.
  - **On success**, it:
    - removes `in` from the carried stack;
    - adds `out` of the output to the workshop's `Stored`;
    - adds `out` to a Make order's `done` for that recipe. At `done >= count` the order is removed and
      `WorkshopOrderDone` is emitted.

  A Keep order and a missing order add nothing; the craft still happens. If a job ends with input left over (an order
  was cut), the rest is dropped at the entrance as a pile and hauled back (JOB-10).
- **CRF-12** Output hauls. Each complete workshop keeps at most one **Unload** job.
  - **Kind** `Unload`, priority **30**.
  - **When:** its unpromised output reaches `haulAt`, or it has any unpromised output and no Craft job, claimed or
    open.
  - **Steps:** `GoToBuilding(workshop) → PickUpFromStorage(workshop, item, n) → GoToBuilding(storage) →
    DeliverTo(storage)` (GoToBuilding reaches the entrance's stand cell like a pump haul, ADR-083).
  - **What it carries:** the lowest item id held, up to `CarryCapacity`.
  - **Storage:** chosen as for JOB-10, reserving StorageIn at it and StorageOut at the workshop.
  - **No room:** nothing is posted, and the workshop's status says so (CRF-13).

  `Unload` is its own job kind, so `Pumps.IsBufferHaul` (a `Haul` of the same step shape) never mistakes it for a
  pump's haul.
- **CRF-13** Workshop status is derived, never stored. `Workshops.StatusOf(sim, b)` returns the first that applies:
  1. `NotBuilt`;
  2. `NoOrders`;
  3. `Working` (a Craft job is claimed);
  4. `OutputFull` (no room for one more cycle of the first wanting order, and no storage has room for its output);
  5. `NoInput` (an order wants output but no storage in reach has one cycle of input);
  6. `Waiting` (a job is open and not yet claimed);
  7. `Done` (every order is satisfied: a Keep order's stock is reached).

  The view shows it (VIEW-28), and `NoInput` names the item ("Sawmill: needs log").
- **CRF-14** Preemption, teardown and falls reuse the existing rules:
  - A crafter preempted by a need (JOB-07) drops the carried input, and it is hauled back.
  - A deconstructed or collapsed workshop cancels its jobs and drops its `Stored` outputs with the refund (BLD-09,
    GRV-07).
  - Craft and Unload jobs are not JOB-12 give-up sources. The region check (PTH-13) keeps unreachable ones unclaimed,
    and the status reads `NoInput` or `OutputFull`.

## Trade wagon (CRF-15..21)

- **CRF-15** The trader is a building definition with an optional `trader` block (`TraderDef?`). There is at most one in
  data:

  ```json
  {
    "id": "trader", "name": "Trade wagon", "footprint": [2, 2, 3], "entrance": [0, 0, -1],
    "cost": {}, "buildTicks": 0, "placement": "ground",
    "storage": { "capacity": 0, "perItemCapacity": 0, "accepts": ["log", "stone", "cutstone"], "receives": false },
    "workers": 0, "producer": null, "setsBlocks": true, "prebuiltOnly": true, "stackable": false,
    "trader": {
      "firstArrival": 3000, "interval": 7200, "stay": 1800,
      "offers": [
        { "give": { "log": 10 },    "get": { "stone": 10 },   "lots": 4 },
        { "give": { "potato": 10 }, "get": { "stone": 12 },   "lots": 2 },
        { "give": { "stone": 10 },  "get": { "log": 10 },     "lots": 3 },
        { "give": { "log": 10 },    "get": { "cutstone": 6 }, "lots": 2 }
      ]
    }
  }
  ```

  - An **offer** is a fixed-rate swap. The colony gives `give` for each lot and gets `get`, up to `lots` lots per visit.
  - **Validation**, each error naming the file and the offer:
    - at most one trader building;
    - it must be prebuilt-only and have an entrance and a storage with `receives: false`;
    - `firstArrival` ≥ 1, and `stay` in 1..`interval` - 1;
    - each offer has exactly one known `give` item and one known `get` item, and they differ;
    - counts are 1..100, and `lots` is 1..20;
    - every `get` item is in the trader's `accepts`.
  - The player-facing name is "Trade wagon". The starting wagon stays "Wagon".
- **CRF-16** The schedule is a pure function of the tick.
  - Visit `v` (v = 0, 1, …) arrives at `firstArrival + v × interval` and leaves `stay` ticks later.
  - On seed 1 that is ticks 3000–4800, 10200–12000 and 17400–19200 before day 10.
  - `Traders.NextArrival(tick)` and `Traders.LeavesAt` feed the HUD.
- **CRF-17** Arrival happens at the Great Hall, at `Traders.Tick` (ARCH-01 step 7, after `Workshops.Tick`), on the
  arrival tick.
  - **Site:** the trader is placed complete with the BLD-15 start-site search around the hall: the nearest dry site
    with a footprint gap of 2..4 whose entrance the hall's entrance reaches. It writes BuildingSolid like
    `PlacePrebuilt`.
  - **Visit state:** each offer's remaining lots reset to `lots`, and the deal list starts empty.
  - **No site:** the visit is skipped, `TraderNoRoom` is emitted, and the next visit tries again. No hall counts as
    no site.
  - **Site clearing (ADR-084):** like a new site (BLD-07), the arrival clears dig marks on the trader's floor, moves
    loose piles in its footprint to its entrance (their pick-ups are cancelled), and moves dwarves in it there too.
  - **Events:** `TraderArrived(building)` on arrival.
  - **Anchored:** the trader is anchored like the Great Hall. It never collapses, its floor is never dug (GRV-06), and
    `Deconstruct` on it is rejected with `PrebuiltOnly`.
- **CRF-18** Command `AcceptOffer(int Offer, int Lots)`, logged, with a `CommandCodec` entry. It appends a deal
  `(offer, lots, paid 0, granted 0)` and takes `Lots` off the offer's remaining lots. Rejections, in this order:
  - `NoTrader`, when no trader is here;
  - `BadOffer`, when the index is out of range;
  - `BadLots`, when `Lots` < 1;
  - `OfferExhausted`, when `Lots` is more than the lots remaining;
  - `NotEnough`, when `Lots × give` is more than the **free stock** of the give item.

  The free stock is `Stored − StorageOut reservations` summed over complete storage buildings other than the trader,
  minus the unpaid units of earlier deals for that item. "Unpaid" means owed and not yet taken by a claimed Trade job:
  units a claimed job has reserved or carries are already out of the storage sum (ADR-084).
- **CRF-19** Payment: dwarves load the give items onto the trader.
  - **Posting:** `Traders.Tick` keeps **Trade** jobs (new kind, priority **35**, below the pump) for every deal in
    order. Each deal has `open = lots × give − paid − carried by claimed jobs of that deal` units still to post. They
    go out in `ceil(open / CarryCapacity)` jobs, BLD-06 style. The source is the nearest complete storage with the
    stock unreserved, not the trader. If no storage has a whole load, the one with the most unreserved stock gives
    what it has (ties to the lower id), as ADR-041 does for Deliver jobs.
  - **Steps:** `GoToBuilding(source) → PickUpFromStorage(source, item, n) → GoTo(trader entrance, Exact) →
    Pay(trader, deal)`.
  - **`WorldActions.Pay`** is a new StepKind. It checks that the trader is here, the agent is on its entrance and it
    carries the give item. It moves `min(carried, unpaid)` into the deal's `paid`.
  - **Granting:** while `paid ≥ (granted + 1) × give` and `granted < lots`, `granted` goes up by 1 and `get` of the
    get item is added to the trader's `Stored`.
  - **Bought goods:** they are the colony's at once. The trader's storage is a normal source (BLD-16), counts in
    `Storage.Totals` and in `Stock`, and a Build or Deliver job may take from it directly.
- **CRF-20** Unloading: `Traders.Tick` keeps **Trade** jobs that carry the trader's unreserved `Stored` to storage.
  - **Steps:** `GoToBuilding(trader) → PickUpFromStorage(trader, item, n) → GoToBuilding(storage) → DeliverTo(storage)`
    (GoToBuilding, as for workshop unloads, ADR-083/084).
  - **Load:** at most `CarryCapacity` per job, lowest item id first.
  - **Storage:** chosen as for JOB-10.
  - **Count:** as many jobs as there are chunks.
- **CRF-21** Departure happens at `Traders.Tick` on the leave tick, in this order:
  1. Every job that names the trader is cancelled. Carried items are dropped where the dwarf stands (JOB-07) and
     hauled later.
  2. For each deal, the paid units of lots not yet granted (`paid − granted × give`) are dropped as a pile of the give
     item at the entrance (a refund).
  3. The trader's remaining `Stored` (bought goods not yet unloaded) is dropped as piles at the entrance. The colony
     keeps what it paid for.
  4. The footprint turns to Air, the building is removed, the visit state is cleared, and `TraderLeft` is emitted.

  The trader leaves on time whatever is still open. Paid units of granted lots leave with it.

## Save, hash and determinism (CRF-22..23)

- **CRF-22** Save and hash.
  - **M11-T4, save v8:**
    - each building saves its workshop orders (count, then `recipe, mode, count, done` in recipe order);
    - the new job kinds (`Craft`, `Unload`) and the `Craft` step are saved with the job board;
    - `SetWorkshopOrder` is in the command log.
    - `BuildingSystem.AddToHash` adds the orders after `Stored`, only for a building with a `workshop` block, so
      buildings without one hash as before.
  - **M11-T5, save v9:** a `Traders` section after the block plans holds the current visit:
    - the trader building id (0 when none);
    - each offer's remaining lots;
    - the deals (`offer, lots, paid, granted`, in order).

    The visit index is not stored; it follows from the tick (CRF-16), and a skipped visit leaves no state.
    `AcceptOffer` is in the command log. `StateHash` adds the section only while a trader is here, so a hash taken
    before the first arrival does not move.
- **CRF-23** Determinism notes:
  - Orders, recipes, offers and deals are lists in a defined order (recipe index, offer index, deal order).
  - Sources and storages are picked by distance and then by building id.
  - The schedule is a function of the tick. No new `Dictionary` is enumerated.
  - Craft cycles and payments are integer counts.
  - Changing the data changes the goldens. The M11-T4 data change (wagon stock, block costs) is a golden
    regeneration and must be recorded.

## Events and view hooks (CRF-24)

- **CRF-24** New `SimEvent`s, for the view only (ARCH-03): `WorkshopOrderDone(building, recipe)`,
  `TraderArrived(building)`, `TraderLeft(building)` and `TraderNoRoom`. The view panels are VIEW-28..30
  (`view-ui.md`).

## Budget

- **CRF-P1** SIM-P1 holds with the economy running: the full `Tick()` median is at most 8 ms on seed 1 with
  `EconomyScript` while a trader is in and both workshops work (M11-T7 perf test).

## Task map

| Task | Implements |
|---|---|
| M11-T4 | CRF-01..14, CRF-22 (v8), the CRF-23 notes and the `WorkshopOrderDone` event. The wagon's refined stock and the new block costs, with golden regeneration. Tests and scripts that build Planks, PolishedStone or Slate get the refined items (tests may stock storage directly; screenshot scripts use the wagon's stock or build a workshop). |
| M11-T5 | CRF-15..21, CRF-22 (v9) and the trader events |
| M11-T6 | VIEW-28..30: the workshop panel, the trade panel, and refined items and alerts in the HUD |
| M11-T7 | `EconomyScript` (seed 1), CRF-P1, and headless and screenshot `--script economy` |

## Acceptance scenarios

1. A Sawmill with a Make 20 planks order and logs in the hall makes exactly 20 planks. They end up in storage, the
   order is removed, and the logs taken equal 10.
2. A Keep 10 cut stone order crafts until the colony holds 10. When a Build job uses some, crafting starts again.
3. A workshop with an order and no input reads `NoInput` and posts no job; adding the input starts it.
4. A crafter who gets thirsty mid-job drops the input and drinks. The input is hauled back, and the order still
   completes.
5. Wood planks, Polished stone and Slate blocks take refined items from storage, and digging them refunds the refined
   item.
6. The trade wagon arrives at tick 3000 beside the hall. Accepting "10 log → 10 stone" twice moves 20 logs into it
   and brings 20 stone into storage. It leaves at 4800, and the footprint is Air again.
7. Accepting more lots than remain, or more than the free stock allows, is rejected with the CRF-18 reason.
8. A deal half paid when the wagon leaves refunds the paid units of the ungranted lot as a pile, and nothing floats.
9. Save and load with a Craft job mid-cycle, and again with a trader in and a deal half paid, continue identically
   (SAV-03).
10. `EconomyScript` on seed 1 builds both workshops, keeps planks and cut stone stocked, trades with one wagon, and
    builds a small hall from refined blocks by day 10 with all 5 dwarves alive.
