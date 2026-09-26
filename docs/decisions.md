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
