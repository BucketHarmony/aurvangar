# Spec — Water (WAT)

Timberborn-style volumetric water: each cell holds an amount; water falls, then spreads sideways toward lower
levels. No pressure, no currents. Integer math only.

## Representation

- **WAT-01** `WaterGrid` holds `ushort[] level` with the same indexing as blocks. Units: `Full = 1024` per cell.
  A cell's level is in `[0, Full]`. Solid cells always hold 0.
- **WAT-02** An **active set** tracks cells that may change next step: a `bool[]` flag array plus an `int[]` list
  of indices. A cell becomes active when its level changes, when a neighbor's level changes, or when a block in
  it or a neighbor changes (via `World` change notification). A cell leaves the active set after a step in which
  neither it nor any neighbor changed.
- **WAT-03** The step is **double-buffered**: flows are computed from the current levels into an `int[] delta`
  accumulator for every active cell, then all deltas are applied. Order of iteration therefore does not affect
  results. The active list is sorted ascending before each step anyway, so logs and debugging are stable.

## Step rules (per active wet cell `c` with level `L > 0`)

- **WAT-04 Fall.** Let `b = c + down`. If `b` is not solid: `move = min(L, Full - level[b])`. Transfer `move`
  from `c` to `b`. Update `L -= move`.
- **WAT-05 Spread.** If after fall `L > 0` and (`b` is solid or `level[b] == Full`): consider the 4 horizontal
  neighbors `n` that are not solid. For each with `level[n] < L`, flow `f_n = (L - level[n]) / 5`
  (integer division). The divisor 5 = 4 neighbors + self, which caps outflow so the cell never goes below its
  neighbors. Transfer `f_n` from `c` to each `n`.
- **WAT-06 Minimum flow.** If `f_n == 0` but `L - level[n] >= 2`, flow 1 unit to the single lowest such
  neighbor (tie-break: lowest index). This lets thin sheets settle instead of freezing with a staircase.
- **WAT-07 Evaporation of films.** A cell with `0 < L < 16` whose below cell is solid and whose 4 horizontal
  neighbors each hold `< 16` loses `L` (set to 0). This kills infinite thin films. Evaporated volume is
  counted in `WaterStats.Evaporated` for conservation tests.
- **WAT-08 No upward flow.** Water never moves up by flow. U-tubes do not equalize. This is intentional for the
  POC. The only upward movement is overfill resolution (WAT-16).
- **WAT-16 Overfill resolution.** Because inflows from several neighbors are computed from the same snapshot, a
  cell can exceed `Full` after deltas apply. After applying, walk overfull cells in ascending `y` then index
  order: push the excess into the cell above if it is not solid, otherwise count it as `Evaporated` and clamp.
  Levels are stored in an `int[]` working buffer during the step so overflow cannot wrap a `ushort`.

## Sources, drains, weather

- **WAT-09** Source cells (from terrain gen) are set to `Full * strength / 100` at the start of each step,
  where `strength` comes from `WeatherSystem` (100 in Wet season, 0 in Drought). Added volume goes to
  `WaterStats.SourceAdded`.
- **WAT-10** Drain cells are set to 0 at the end of each step. Removed volume goes to `WaterStats.Drained`.
- **WAT-11** Conservation: over any run, `initial + SourceAdded - Drained - Evaporated - Pumped == current total`
  exactly. Tested.

## Interaction with the world

- **WAT-12** When a cell becomes solid (building placed, levee built), its water is pushed: distribute its level
  to non-solid horizontal neighbors equally, remainder to the cell above; any remaining overflow is deleted
  and counted as `Evaporated`. Then level = 0.
- **WAT-13** When a cell becomes non-solid (dig), it and its 6 neighbors become active.
- **WAT-14** Water depth for agents: a standable cell is **wadeable** if `level[c] < Full/2`, else **deep**.
  Deep cells are not walkable (see `pathfinding.md`). An agent already standing in a cell that becomes deep
  immediately requests a path to the nearest non-deep standable cell (flee). If none reachable within 64 steps,
  it takes 1 health damage per tick until it reaches one or dies (drowning, `DeathCause.Drowned`).
- **WAT-15** `WaterDirty(chunk)` events fire for chunks whose any cell level changed by ≥ 32 units since the last
  emitted event for that chunk (to cap remesh frequency). Also fire when a cell crosses 0 ↔ >0.

## Rendering contract (view side, for reference)

- Surface height of a wet cell whose above cell is dry = `y + level/Full`. Build one mesh per chunk of top faces
  at that height, plus side faces where a neighbor's surface is lower. Color depth-tinted by `level`.

## Budgets

- **WAT-P1** Step time ≤ 4 ms with 20,000 active cells (Release build, perf test machine).
- **WAT-P2** A settled river on seed 1 has ≤ 3,000 active cells after 1,200 ticks.

## Acceptance scenarios (tests)

1. Single cell of `Full` on a flat floor spreads and evaporates to 0 within 400 steps, conservation holds.
2. 10×10×3 basin with a source of `Full` per step at one corner fills to depth 3 ±1 cell-level (1024 units) within
   2,000 steps, then overflows the rim.
3. Water poured into a 1-wide, 5-deep shaft fills from the bottom up (level of y=1 reaches Full before y=2 > 0).
4. Breach: a 5×5 reservoir at depth 2 separated from a dry 10-long tunnel by a 1-block wall. Remove the wall.
   Within 300 steps the tunnel's far end has level > 0.
5. Placing a levee in a wet cell conserves volume per WAT-12.
6. Drought: source strength 0 → river total volume drops by ≥ 70% within 2,400 ticks on seed 1.
