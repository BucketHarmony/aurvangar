# Spec — World (WLD)

## Storage

- **WLD-01** World size is fixed at construction: `SizeX=128, SizeY=64, SizeZ=128`. Tests may construct smaller
  worlds (any multiple of 32 on X and Z, any multiple of 32 on Y, minimum 32×32×32).
- **WLD-02** Blocks are stored as one `byte[]` of length `SizeX*SizeY*SizeZ`, value = `BlockId`. Index:
  `x + z*SizeX + y*SizeX*SizeZ` (Y-major layers so a Y-slice is contiguous).
- **WLD-03** Chunks are a view concept: chunk size 32³, `ChunkIndex(cell) = (x>>5) + (z>>5)*CX + (y>>5)*CX*CZ`.
  The world tracks a dirty flag per chunk and emits `ChunkDirty` when any cell in the chunk changes. A change on a
  chunk border also dirties the neighbor chunk (faces change).
- **WLD-04** Out-of-bounds reads return `BlockId.Bedrock` for `y < 0` and `BlockId.Air` elsewhere. Out-of-bounds
  writes are rejected.
- **WLD-05** `y = 0` is always Bedrock and cannot be dug.

## Block types (from `data/blocks.json`)

| Id | Name | Solid | Diggable | Hardness (ticks) | Drop |
|---|---|---|---|---|---|
| 0 | Air | no | – | – | – |
| 1 | Bedrock | yes | no | – | – |
| 2 | Stone | yes | yes | 60 | 1 stone |
| 3 | Dirt | yes | yes | 25 | – |
| 4 | Grass | yes | yes | 25 | – |
| 5 | Sand | yes | yes | 20 | – |
| 6 | Farmland | yes | yes | 25 | – |
| 7 | BuildingSolid | yes | no | – | – |

- **WLD-06** `BuildingSolid` is written into every footprint cell of a completed building (and Levee). It is solid
  for water and pathing. It is removed only by `BuildingSystem` on deconstruction.
- **WLD-07** Trees and bushes are **not** blocks. They are plant entities anchored to a cell (see
  `needs-economy.md`). A cell holding a tree trunk is non-standable and non-diggable until the tree is chopped.
  Water passes through plant cells.

## Terrain generation (WLD-GEN)

Deterministic from `seed`. Implemented in `TerrainGenerator`, pure function of `(seed, size)`.

- **GEN-01** Base height: 2D value noise (own implementation, integer hash based, no library), 2 octaves,
  heights in `[18, 26]`. Use `Rng` derived from `seed` only.
- **GEN-02** Hill: add a radial bump centered at `(90, 40)` (x,z), radius 26, peak +16. Clamp total height ≤ 48.
- **GEN-03** River: a channel from `x=0` to `x=127` following `z = 80 + 8*sin(x/20)` computed with integer
  approximation (precomputed table from `Fixed` sine, not `Math.Sin`). Channel half-width 3, bed at
  `baseHeight - 4` along its path. Banks slope 1 block per cell for 2 cells.
- **GEN-04** Layers per column of height h: `y=0` Bedrock; `1 ≤ y < h-4` Stone; `h-4 ≤ y < h` Dirt;
  `y = h` Grass (Sand if within 2 cells of the river channel).
- **GEN-05** Spawn flat: a 9×9 area centered at `(40, 60)` flattened to the median height of that area. The
  Colony Hub is pre-placed at its center (see `buildings.md`). If the river passes within 6 cells of it on seed 1,
  shift the area north in 4-cell steps until it does not.
- **GEN-06** Trees: Poisson-disk style placement with min spacing 4 on Grass cells not in the spawn flat, not
  within 3 of the river; target 150 trees. Tree = trunk height 4 (occupies 4 cells), canopy is visual only.
- **GEN-07** Berry bushes: 24 bushes on Grass within 4–12 cells of the river and within 30 cells of the hub.
- **GEN-08** Water sources: column cells `x=0`, `z` within the river channel, `y` from bed to `bed+3`.
  Drains: `x=127`, same z range, all y. Initial water: fill the channel to `bed+3` at world creation and run
  the water sim for 600 ticks inside `WorldFactory` so the river is settled at tick 0.

## Invariants (checked by `WorldInvariantTests`)

- Every column has Bedrock at `y=0`.
- The hub footprint cells are `BuildingSolid` and the cells in front of its entrance are standable.
- At least one path exists from the hub entrance to the river bank and to the hill.
- The same seed produces byte-identical block arrays.
