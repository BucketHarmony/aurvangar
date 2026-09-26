# 02 — Conventions

## C#

- Nullable enabled, warnings as errors (set in `Directory.Build.props`). File-scoped namespaces.
- Namespaces mirror folders: `Aurvangar.Sim.Water`, `Aurvangar.Sim.Jobs`, …
- Value types for coordinates and ids: `Int3`, `AgentId`, `BuildingId`, `JobId`, `PlantId`, `ItemId`, `BlockId`.
  Ids are `readonly record struct` wrappers over `int`. Id 0 is invalid.
- Hot-path data (blocks, water, path grid) lives in flat arrays indexed by `World.Index(Int3)`
  (`x + z * SizeX + y * SizeX * SizeZ`). No per-cell objects.
- No LINQ in per-tick code paths. LINQ is fine in setup, tests, and tools.
- No `float`/`double` in sim state. Use ints and fixed-point (`Fixed` helpers in `Core/`). The view converts to
  `float` for rendering.
- No exceptions for control flow. Actions return `ActionResult`; commands emit `CommandRejected`.
- Public sim types get an XML doc summary of one line. Spec IDs go in comments where the rule is implemented:
  `// WAT-04: lateral flow equalizes toward lower neighbors`.

## Determinism checklist (review every sim diff against this)

- [ ] No `Dictionary`/`HashSet` enumeration in tick code unless the result is order-independent (sums, any/all).
      Use `SortedDictionary`, sorted arrays, or index-ordered arrays.
- [ ] No `System.Random`, `Guid`, `DateTime`, `Stopwatch` in sim logic (Stopwatch allowed only in perf counters
      that do not feed back into state).
- [ ] No floating-point state.
- [ ] Every new piece of state is serialized in `SaveGame` and included in `StateHash()`.
- [ ] Agents processed in ascending id order; jobs chosen with deterministic tie-breaks (priority, distance, job id).

## Godot project

- Scenes in `src/Aurvangar.Godot/scenes/`, scripts in `src/Aurvangar.Godot/scripts/` mirroring scene names.
- Root namespace of the Godot project is `Aurvangar.Client` (ADR-006). Never `Aurvangar.Godot`.
- One `GameRoot` autoload-free root scene (`Main.tscn`). No autoload singletons.
- UI built with Godot Control nodes in scenes, logic in C#. No GDScript.
- Placeholder art only: flat-colored `StandardMaterial3D`, box meshes for buildings, capsule for colonists.
  Colors come from `data/palette.json`.

## Git

- One backlog task per commit. Message: `M<m>-T<t>: <title>`.
- Never commit `artifacts/`, `.godot/`, `bin/`, `obj/`.
- Never rewrite history.

## Tests

- Test class per system: `WaterGridTests`, `PathfinderTests`, … Scenario tests live in `Scenarios/`.
- Test names: `Method_Condition_Expected` or a plain sentence for scenarios.
- Categories via `[Trait("Category", "...")]`: `Unit` (default), `Scenario`, `Golden`, `Perf`.
- `check.sh` runs everything except `Perf`. `perf.sh` runs `Perf`.
