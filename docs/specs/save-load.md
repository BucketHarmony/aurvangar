# Spec — Save / load (SAV)

- **SAV-01** Binary format via `BinaryWriter`: magic `CSAV`, format version `int` (start at 1), then sections in
  fixed order: header (seed, tick, rng state), blocks (RLE of the byte array), water (RLE of ushort levels),
  water stats, plants, moisture flags (saved and hashed: they reflect the water at the last 50-tick recompute, ADR-046),
  farm tiles, buildings (sorted by
  id), storage contents, item piles (sorted by cell index), designations, agents (sorted by id, alive only), job
  board (sorted by id, with reservations), JOB-12 give-up marks (sorted by source and id; format version 5, M7-T5),
  weather, id allocators, command log. M8-T2 appends a `BlockPlans` section after the give-up marks (format
  version 6, CON-04). M11-T10 adds a `BlockForms` section right after the blocks, a form byte per plan entry and the
  form in `DesignateBuild` commands (format version 7, CON-22). M11-T4 adds workshop orders to buildings, the `Craft` and
  `Unload` jobs and `SetWorkshopOrder` (format version 8, CRF-22); M11-T5 adds a `Traders` section after the block
  plans and `AcceptOffer` (format version 9).
- **SAV-02** Load constructs a `Simulation` purely from the file plus `ContentDb`. No terrain generation runs on
  load.
- **SAV-03** Round-trip rule: `Save(sim) → Load → StateHash()` equals the original `StateHash()`, and ticking
  both forward 1,000 ticks with the same commands produces equal hashes at every 100 ticks.
- **SAV-04** Version mismatch → load fails with a clear error. No migration in the POC.
- **SAV-05** Save size for seed 1 at day 5 ≤ 3 MB.
- **SAV-06** Dead agents are dropped on save (JOB-02); `StateHash` excludes dead agents so SAV-03 holds.
