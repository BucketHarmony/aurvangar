# Decisions (ADR log)

Short records of choices made during the build. Newest at the bottom. Claude Code adds an entry whenever a spec is
ambiguous or wrong, a budget forces a design change, or a hard rule needs an exception.

Template:

```
## ADR-NNN: <title> (<date>, <task id>)
Context: <what forced the decision>
Decision: <what we do>
Consequences: <what this makes easier/harder; follow-ups>
```

---

## ADR-001: Timberborn-style prefab construction, indirect control (2026-09-25, scaffold)
Context: The references split between direct-control (Minecraft) and indirect-control colony sims.
Decision: Indirect control only. Buildings are prefabs placed as blueprints; terrain is shaped by dig designations
(DF style) and levees. Players and colonists share the WorldActions API so a player avatar can be added later as an
Agent driven by input.
Consequences: No avatar, no freeform block placement except levees in the POC.

## ADR-002: Godot 4.6 .NET, C# only, sim as a Godot-free library (2026-09-25, scaffold)
Context: Carried over from the project's earlier decisions. Godot 4.7 exists; 4.6 is kept for stability of the
decision record. GodotSharp 4.6.x targets net8.0.
Decision: Godot.NET.Sdk/4.6.2, net8.0 everywhere, Aurvangar.Sim and Aurvangar.ViewCore never reference Godot.
Consequences: Upgrading to 4.7 is a one-line csproj change plus project.godot features; do it only via a new ADR.

## ADR-003: Game data embedded in Aurvangar.Sim (2026-09-25, scaffold)
Context: Sim, tests, headless tool and Godot all need the same data without path logic.
Decision: data/*.json are EmbeddedResources of Aurvangar.Sim, loaded by ContentDb.LoadEmbedded().
Consequences: Editing data requires a rebuild. Acceptable for the POC; modding is out of scope.

## ADR-004: Integer-only sim state and fixed-point water (2026-09-25, scaffold)
Context: Determinism across runs and machines is required for golden tests, save/load equality and replay.
Decision: No float/double in Aurvangar.Sim (enforced by scripts/sim-guard.sh). Water in 1/1024 cell units.
Trigonometry from a literal integer table (Core/Fixed).
Consequences: Slightly more verbose math. Palette floats live in Content/Defs.cs, which is exempt because the sim
never reads them.

## ADR-005: Thirst and a pump added to the MVP (2026-09-25, scaffold)
Context: The earlier scope had hunger only, with water mattering only through farm moisture.
Decision: Add thirst and the Water Pump. Water then matters directly, and the drought creates the core Timberborn
tension (store water or build a reservoir).
Consequences: One more need and one more building. Both are small.

## ADR-006: Godot C# namespace is Aurvangar.Client (2026-09-25, scaffold)
Context: A namespace named Aurvangar.Godot would shadow the Godot namespace inside the project.
Decision: The folder and csproj are Aurvangar.Godot; the root namespace is Aurvangar.Client.
Consequences: None beyond naming.

## ADR-007: Godot project is not in Aurvangar.sln (2026-09-25, scaffold)
Context: Godot.NET.Sdk must resolve from NuGet for any tool that loads the solution; the sim, tests and tools do not
need it.
Decision: Aurvangar.sln contains Sim, ViewCore, Tests, Headless. check.sh builds the Godot csproj separately.
Consequences: IDEs should open Aurvangar.sln for sim work and the Godot project folder for view work.

## ADR-008: Name and theme: Aurvangar, dwarves (2026-09-26, scaffold)
Context: The project needed a name before its namespaces spread. The owner chose a dwarf theme and a Norse source.
Decision: Title, repo, solution and namespaces are Aurvangar (`Aurvangar.Sim`, `Aurvangar.ViewCore`,
`Aurvangar.Client`). Colonists are dwarves in all player-facing text; code identifiers stay generic. The long-term
endgame (science, exploration, diplomacy) is recorded in docs/00-overview.md as direction only.
Consequences: Player-facing strings (UI labels, names) use dwarf flavor from M4-T11 onward. No Tolkien-derived words.

## ADR-009: River channel geometry and "river distance" (2026-09-26, M1-T3)
Context: GEN-03 says "bed at baseHeight - 4" and "banks slope 1 block per cell for 2 cells", and GEN-04/06/07
measure distances "from the river" without defining them. A bed that follows the local noise height would make
pools and dams along the channel; banks that stop after 2 cells leave a 2+ block cliff (terrain sits ≥ 4 above the
bed), so colonists could not walk down to the water (pump, DoD step 2).
Decision: `baseHeight` is GEN-01's minimum base height (18). The channel (|z - center(x)| ≤ 3) has a constant floor:
the lowest water cell is y = 14 ("bed"), the solid floor top is y = 13; GEN-08 sources and the initial fill cover
y = 14..17 ("bed .. bed+3"). Banks slope up 1 block per cell from the channel edge until they meet natural terrain.
"Distance from the river" is `max(0, |z - center(x)| - 3)` (cells from the channel edge along Z): Sand where ≤ 2,
no trees where ≤ 3, bushes where 4..12. Bushes' "within 30 cells of the hub" is Chebyshev on X/Z from the spawn
flat center. Drains cover the channel z range at x = 127 for y = bed .. top of the world. The hub is centered on
the flat using its footprint from data (`TerrainResult.HubOrigin(footprint)`; its entrance faces north onto the flat).
Consequences: The river is fed/drained by level difference only (flat bed); the M2-T5 settle and WAT-P2 budget are
measured on this geometry. Initial water also fills the lower bank cells (y ≤ 17), so the wet surface is wider
than the 7-cell channel.

## ADR-010: Water active set holds wet cells only and is hashed state (2026-09-26, M2-T1)
Context: WAT-02 says a cell becomes active when it or a neighbor changes, but every step rule (WAT-04..07) applies
only to wet cells, and activating dry air/solid neighbors would inflate `ActiveCount` against the WAT-P2 budget.
Separately, which cells step next affects the outcome (a cell outside the set is not re-evaluated), so the set is
state, not a cache.
Decision: `Activate` admits only cells with level > 0 (solid cells always hold 0). A dry cell that receives water
changes level and is activated then. A cell that dries during a step is not re-flagged; one dried by `SetLevel`
while already flagged stays in the set until the next step, which drops it.
`WaterGrid.AddToHash` includes the active set as sorted indices. Save/load (M4-T10) must persist the active set
rather than recomputing it.
Consequences: `ActiveCount` counts wet cells that may change. M2-T4 dig activation (WAT-13) works through the wet
neighbors of the dug cell. Adding the (empty) set to the hash changed the seed-1 golden hashes.

## ADR-011: Spread/film details, sources raise only, and the shaft test's "bottom up" (2026-09-26, M2-T2)
Context: (1) WAT-06 does not say whether minimum flow competes with normal spread flows; WAT-07 does not say which
level it tests or whether it runs before fall. (2) WAT-09 says sources are "set to" `Full * strength / 100`, but
lowering a cell would remove volume that no `WaterStats` counter records, breaking WAT-11. (3)
`WaterScenarioTests.ShaftFillsBottomUp` asserted every tick that each wet shaft cell has a Full cell below it. That
cannot hold with WAT-04 (one cell per step, as `Fall_MovesOneCellPerStep` requires): the first water is over empty
cells while it falls. The double-buffered step (WAT-03) also delivers a source's stream as separate Full slugs,
because the source cell cannot fall into a cell that is Full in the snapshot.
Decision: (1) All rules read the pre-step snapshot. WAT-07 is checked first, on the cell's snapshot level, and
applies only when the floor is solid (so a falling film never evaporates); solid and out-of-world neighbors count
as 0. Minimum flow sends 1 unit to the lowest neighbor whose flow rounded to 0 with a difference of ≥ 2 (ties: lowest
index), even if other neighbors received normal flow that step. Out-of-world horizontal neighbors are walls.
(2) Sources only raise a cell to the target (added volume to `SourceAdded`); they never lower it. Sources are applied
in M2-T2 because `ShaftFillsBottomUp` needs them; drains stay in M2-T3. (3) The shaft test now asserts that no water
ever sits over a partially filled shaft cell, the bottom cell is 0 or Full every tick, and the whole shaft is Full at
the end.
Consequences: Overfill (WAT-16) can occur from spread alone (four min-flow inflows of 1 into a cell 2 below Full);
tests cover both the open-air push and the ceiling evaporation. In Drought (strength 0) sources add nothing and the
river drains through drains and evaporation only.
