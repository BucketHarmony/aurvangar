# Spec — Designations (DSG)

- **DSG-01** `Designations` stores a `byte[]` per cell: `None`, `Dig`, `DigUnreachable`. Chop designations are
  stored on the tree entity (`tree.MarkedForChop`). Farm tiles live in `FarmSystem`.
- **DSG-02** `DesignateDig(box)`: for every cell in the box that is diggable (WLD block table) and not under a
  building, set `Dig`. Cells already designated are unchanged. Air cells in the box are ignored.
- **DSG-03** `DesignationSystem.Tick` posts one Dig job per designated cell that has no open job and is
  **exposed** (at least one of its 6 neighbors is non-solid). Unexposed cells wait; digging their neighbor
  exposes them. This produces DF-style tunneling from the surface inward.
- **DSG-04** Dig job ordering bias: among equal-priority dig jobs, JOB-06's distance tie-break naturally
  digs from the near side. Additionally, dig jobs for cells with higher `y` get +1 priority per level above
  the lowest designated `y` in the same job batch, so pits are dug top-down.
- **DSG-05** `DesignateChop(area)`: marks every tree whose base cell is inside the XZ rectangle (any Y).
- **DSG-06** `CancelDesignation(box)`: clears Dig marks, unmarks trees, removes farm tiles whose crop is
  `Empty` (Farmland stays Farmland), and cancels open jobs for those targets (claimed jobs release and the
  agent goes idle).
- **DSG-07** After a Dig completes the designation is cleared.
- **DSG-08** A dig that would remove the block an agent is standing on is deferred while any agent stands there.
- **DSG-09** (M4-T14, ADR-037) A dig is never taken or finished from a stand cell that the dig would cut off from
  the Great Hall's region. Such a dig waits; once no dig is in progress and no open dig has a safe stand cell in a living agent's region, each
  dig whose stand cells all strand is dropped and its mark turns `DigUnreachable`. No automatic stairs.
