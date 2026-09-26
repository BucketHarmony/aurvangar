# CLAUDE.md — Colony Sim POC

Working title: **Colony Sim**. A voxel colony builder in the Timberborn vein: indirect control, prefab
buildings, water as the central system, DF-style digging. This file is the operating manual for Claude Code.
Read it fully at the start of every session.

## Stack (fixed — do not change without an ADR in docs/decisions.md)

- Godot **4.6** .NET edition, `Godot.NET.Sdk/4.6.2`, C# only. **No GDScript anywhere.**
- .NET 8 (`net8.0`) for every project.
- `src/Colony.Sim` — the entire simulation. Plain .NET library. **Must never reference Godot.**
- `src/Colony.ViewCore` — pure C# view logic with no Godot dependency: chunk mesher, slicing rules, water
  surface builder. Unit-tested like the sim.
- `src/Colony.Godot` — the Godot project. Scene nodes, input, UI. Turns ViewCore output into Godot meshes.
  Reads sim state, sends commands.
- `tests/Colony.Sim.Tests` — xUnit. All gameplay correctness is proven here, headless.
- `tools/Colony.Headless` — console runner: run a seed for N ticks, print stats, dump state hash, profile.

## Where things are

| Need | Read |
|---|---|
| What we are building and what is out of scope | `docs/00-overview.md` |
| Layering, tick loop, commands, events, determinism | `docs/01-architecture.md` |
| Code rules | `docs/02-conventions.md` |
| System behavior | `docs/specs/*.md` (each rule has an ID like `WAT-07`) |
| How to test, scenario DSL, golden hashes, perf, screenshots | `docs/testing.md` |
| The work queue | `BACKLOG.md` |
| What has been done, session by session | `PROGRESS.md` |
| Decisions made during the build | `docs/decisions.md` |
| Game data (blocks, items, buildings) | `data/*.json` |

## The work loop

Run this loop until you hit a `HUMAN-GATE` task or the backlog is empty.

1. Read the last 3 entries of `PROGRESS.md` and the whole of `BACKLOG.md`.
2. Pick the first unchecked task whose `deps` are all checked. Skip nothing; do tasks in order within a milestone.
3. Read every spec ID the task references. Read the existing code the task touches.
4. Write or un-skip the acceptance tests first (`[Fact(Skip = "M3-T2")]` → `[Fact]`). Run them and confirm they fail for the right reason.
5. Implement until `./scripts/check.sh` is fully green. Never weaken, skip, or delete a test to get green. If a test is wrong, fix the test and record why in `docs/decisions.md`.
6. Check the task box in `BACKLOG.md`. Append a `PROGRESS.md` entry (template at the top of that file).
7. Commit: `git add -A && git commit -m "M3-T2: <task title>"`. One task per commit.
8. Go to 1.

If a spec is ambiguous or wrong, pick the simplest option consistent with `docs/00-overview.md`, record it as an
ADR in `docs/decisions.md`, and continue. Do not stop to ask. Stopping is reserved for `HUMAN-GATE` tasks and for
the conditions in "When to stop" below.

## Hard rules

- **Sim/view boundary.** Nothing in `Colony.Sim` references Godot, `System.Numerics` floats for state, wall-clock
  time, threads, or `Random`. The view never mutates sim state directly; it sends `ICommand`s.
- **Determinism.** Sim state is integers only. RNG is `Colony.Sim.Core.Rng` seeded from the world seed. Iterate
  collections in a defined order (sorted ids or indices, never `Dictionary`/`HashSet` enumeration order).
  Same seed + same command log ⇒ same `StateHash()` on every run. `GoldenHashTests` enforces this.
- **One action API.** Every world mutation made on behalf of an agent goes through `WorldActions`
  (`Dig`, `PlaceBlock`, `PickUp`, `Drop`, `Work`, …). Jobs call it; a future player avatar will call it. No system
  writes blocks or items by any other route.
- **Data-driven content.** Block types, items, and buildings are defined in `data/*.json` and loaded into
  `ContentDb`. Do not hard-code building stats in C#.
- **Performance budgets** (see `docs/testing.md`) are tests, not aspirations. A budget failure is a red build.
- **No new NuGet dependencies.** The csproj files list everything allowed. Adding a package needs an ADR and is
  denied by `.claude/settings.json`; stop and write it up in PROGRESS.md instead.
- Keep files under ~400 lines. Split by responsibility when they grow.

## Commands

```bash
./scripts/check.sh          # format check + build all + unit/scenario tests. Must pass before every commit.
./scripts/perf.sh           # perf-category tests (budgets). Run at the end of every milestone and after touching water/paths.
./scripts/run-headless.sh --seed 1 --ticks 24000   # full-sim smoke run with stats
./scripts/screenshot.sh     # renders fixed camera shots to artifacts/screens (needs Godot 4.6 mono + xvfb)
UPDATE_GOLDEN=1 dotnet test tests/Colony.Sim.Tests --filter Category=Golden   # only when a change is intentional; record in PROGRESS.md
```

`GODOT_BIN` must point at the Godot 4.6 .NET editor binary for screenshot and editor tasks. If it is missing,
skip screenshot steps, note it in PROGRESS.md, and continue.

## When to stop

- You reached a task tagged `HUMAN-GATE`. Write the gate report it asks for in `PROGRESS.md` and stop.
- The same test has failed after 3 materially different fix attempts. Write what you tried in `PROGRESS.md`
  under a `BLOCKED` heading, add a `BLOCKED` note to the task, and move to the next task that does not depend on it.
- A task would require breaking a hard rule above.

## Subagents and commands

- `/next-task` — runs one iteration of the work loop.
- `/milestone-check` — verifies a milestone's exit criteria and writes the milestone report.
- `sim-reviewer` agent — review a diff against the hard rules (determinism, boundary, action API). Use it before
  committing any change to `Colony.Sim` that touches more than ~150 lines.
- `perf-auditor` agent — use when a perf test fails or a budget is within 20% of its limit.
