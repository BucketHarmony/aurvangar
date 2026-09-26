# Spec — Movement and pathfinding (PTH)

## Walkability

- **PTH-01** A cell `c` is **standable** if: `c` is not solid, `c` holds no tree trunk, `c + up` is not solid,
  and `c + down` is solid (any solid block including `BuildingSolid`).
- **PTH-02** A standable cell is **walkable** if it is not deep water (WAT-14) and not reserved as an
  in-progress construction footprint cell (construction sites block movement once the first material is delivered).
- **PTH-03** `PathGrid` caches a `byte[] flags` per cell (Standable, Walkable, Wet). It updates lazily: a block or
  water change marks the cell and its 3×3×3 neighborhood dirty; dirty cells recompute on next query.

## Neighbors and costs

- **PTH-04** From walkable `a`, candidate moves to `b` in the 8 horizontal directions at `dy ∈ {-1, 0, +1}`.
- **PTH-05** Step up (`dy=+1`) requires `a + up + up` not solid (headroom to climb). Step down (`dy=-1`) requires
  `b + up + up` not solid.
- **PTH-06** Diagonal moves require both orthogonal intermediates at the same `y` as `a` (for dy=0 or down) or
  `b` (for up) to be walkable. No corner cutting.
- **PTH-07** Cost: orthogonal 10, diagonal 14, step up +6, step down +2, wadeable water +8.
- **PTH-08** Falls are not traversed. Agents never path across a drop of 2 or more.

## Algorithm

- **PTH-09** A* with octile heuristic on (x,z) plus `|dy| * 2`. Binary heap keyed by `(f, h, index)` for
  deterministic tie-breaks. Pooled node arrays sized to the world (`int[] gScore`, `int[] cameFrom`, generation
  stamp array to avoid clearing).
- **PTH-10** Search limit: 20,000 expanded nodes. Exceeding it returns `PathResult.TooFar`.
- **PTH-11** Multi-goal search: `FindPath(start, IReadOnlyList<Int3> goals)` returns a path to the cheapest goal.
  Used for "go to any cell adjacent to target".
- **PTH-12** Paths are `Int3[]` of cells including start and end.

## Regions (reachability)

- **PTH-13** `Regions` assigns a region id to every walkable cell by flood fill using the same neighbor rules.
  Recomputed fully when flagged dirty (any walkability change) at the end of the tick. Job assignment rejects
  jobs whose target-adjacent cells have no region in common with the agent's region, without running A*.
- **PTH-14** Region rebuild budget: ≤ 25 ms on a 128×64×128 world (Perf test). If exceeded, implement per-chunk
  incremental rebuild (record an ADR).

## Following a path

- **PTH-15** Agents move one cell per `MoveTicks` (orthogonal 4 ticks, diagonal 6 ticks, +2 for step up, +3 in
  wadeable water). Position is `Int3 cell` plus `int moveProgress` toward `nextCell`; the view interpolates.
- **PTH-16** Before entering the next cell, the agent re-checks walkability. If blocked, it repaths once to the
  same goal. If that fails, the job step fails (job returns to board, reservation released).
- **PTH-17** Agents do not collide with each other.

## Budgets

- **PTH-P1** 95th percentile A* for 100-cell paths on seed 1 ≤ 1.5 ms.
- **PTH-P2** Region rebuild on seed 1 ≤ 25 ms.

## Acceptance scenarios

1. Straight corridor: path length and cost match hand-computed values.
2. Step up of 1 succeeds; step up of 2 fails (`NoPath`).
3. Diagonal around a corner is not taken when one orthogonal is solid.
4. A pit (drop of 2) is not descended.
5. Deep water cell blocks the only corridor → `NoPath`; lower the water to wadeable → path found with water cost.
6. Regions: two areas separated by a wall have different ids; digging the wall merges them after the tick.
7. Agent standing in a cell that floods to deep flees to the nearest dry cell (WAT-14).
