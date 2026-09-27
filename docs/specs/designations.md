# Spec — Designations (DSG)

- **DSG-01** `Designations` stores a `byte[]` per cell: `None`, `Dig`, `DigUnreachable`. Chop designations are
  stored on the tree entity (`tree.MarkedForChop`). Farm tiles live in `FarmSystem`.
- **DSG-02** `DesignateDig(box)`: for every cell in the box that is diggable (WLD block table) and not under a
  building, set `Dig`. Cells already designated are unchanged. Air cells in the box are ignored. (M4-T15, ADR-038)
  Cells under plants are marked too; their dig job waits until the plant is gone (DSG-03).
- **DSG-03** `DesignationSystem.Tick` posts one Dig job per designated cell that has no open job and is
  **exposed** (at least one of its 6 neighbors is non-solid). Unexposed cells wait; digging their neighbor
  exposes them. This produces DF-style tunneling from the surface inward. (M4-T15, ADR-038) No job is posted for a
  cell with a plant on top; once the plant is felled or removed it is posted as usual. While a tree marked for
  chopping (not given up) stands on a `Dig`-marked floor F, a cell at horizontal (Chebyshev) distance r >= 1 from F
  with y <= F.y - r gets no job either (an unclaimed one is withdrawn), so the tree stays in a chopper's reach.
- **DSG-04** Dig job ordering bias: among equal-priority dig jobs, JOB-06's distance tie-break naturally
  digs from the near side. Additionally, dig jobs for cells with higher `y` get +1 priority per level above
  the lowest designated `y` in the same job batch, so pits are dug top-down.
- **DSG-05** `DesignateChop(area)`: marks every tree whose base cell is inside the XZ rectangle (any Y).
- **DSG-06** `CancelDesignation(box)`: clears Dig marks, unmarks trees, removes farm tiles whose crop is
  `Empty` (Farmland stays Farmland), and cancels open jobs for those targets (claimed jobs release and the
  agent goes idle).
  M8-T2 (CON-07): it also removes block plan entries in the box and cancels the Build jobs that hold them.
  M8-T3 (CON-10): no job is posted for a marked cell that a built block depends on for support; it waits and is
  never turned `DigUnreachable` for that. `DesignateDeconstructBlocks(box)` (CON-18) marks only built blocks.
- **DSG-07** After a Dig completes the designation is cleared.
- **DSG-08** A dig that would remove the block an agent is standing on is deferred while any agent stands there.
- **DSG-09** (M4-T14, ADR-037) A dig is never taken or finished from a stand cell that the dig would cut off from
  the Great Hall's region. Such a dig waits; once no dig is in progress and no open dig has a safe stand cell in a living agent's region, each
  dig whose stand cells all strand is dropped and its mark turns `DigUnreachable`. No automatic stairs.
  M7-T6 (ADR-059): a dig is also not taken or finished while it would cut any other living dwarf (its cell or the
  cell it steps into) off from the hall. Such a dig waits (it is not given up for that), and an idle dwarf with
  nothing to do in the pocket walks out towards the hall.
