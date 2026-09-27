# Testing

Claude Code cannot judge game feel. Everything that can be expressed as a headless assertion is a test.
What remains (does it look right, is it fun) goes to human gates.

## Layers

| Category | Where | Runs in | Purpose |
|---|---|---|---|
| `Unit` | `tests/Aurvangar.Sim.Tests/<System>Tests.cs` | `check.sh` | One rule per test, tiny worlds |
| `Scenario` | `tests/Aurvangar.Sim.Tests/Scenarios/` | `check.sh` | Multi-system behavior on built or generated worlds |
| `Golden` | `tests/Aurvangar.Sim.Tests/GoldenHashTests.cs` | `check.sh` | Determinism lock: seed 1 hash at fixed ticks |
| `Perf` | `tests/Aurvangar.Sim.Tests/Perf/` | `perf.sh` (Release) | Budgets from specs (`*-P*` IDs) |
| Mesher | `tests/Aurvangar.Sim.Tests/Meshing/` | `check.sh` | Greedy mesher quad counts and slicing; `Aurvangar.ViewCore` is referenced by the test project |
| Screenshots | `scripts/screenshot.sh` | manual / gates | Visual regression for human review |

## Scenario DSL

`ScenarioBuilder` builds small worlds from ASCII layers. Each layer is a list of strings (rows = Z, cols = X),
bottom layer first:

```csharp
var sim = new ScenarioBuilder(sizeX: 32, sizeY: 32, sizeZ: 32)
    .Ground(topY: 4)                 // bedrock at y=0, stone y=1..4 everywhere
    .Layer(5,                        // one char per cell; row = z, column = x
        "SSSSS",                     // S stone, . air, W full water, w half water, # BuildingSolid,
        "S...S",                     // T tree base, b berry bush, space = leave unchanged
        "S.W.S",
        "S...S",
        "SSSSS")
    .Source(new Int3(2, 7, 2))
    .Agent(at: new Int3(1, 5, 1))    // M4-T4
    .Hub(origin: new Int3(10, 5, 10))// M4-T8
    .Stock("water", 10)              // M4-T8
    .Build();
sim.RunTicks(300);
```

The legend lives in `ScenarioBuilder.Legend`. Unknown characters throw.

## Golden hashes

- `GoldenHashTests` generates seed 1, applies `Scripts.SurvivalScript` (a fixed command list), runs to tick
  6000, and compares `StateHash()` at ticks 0, 1200, 3000, 6000 with `tests/golden/seed1.txt`.
- A hash change means behavior changed. If the change is intended, regenerate with
  `UPDATE_GOLDEN=1 dotnet test --filter Category=Golden` and write one line in PROGRESS.md saying why.
- A second test runs the same thing twice in-process and asserts equality (catches hidden nondeterminism
  even when goldens are being regenerated).

## Perf

- Run in Release: `dotnet test -c Release --filter Category=Perf`.
- Each perf test warms up, runs N iterations, asserts the budget on the median (and p95 where specified).
- Budgets assume a mid-range desktop. On a slower CI runner set `PERF_SCALE=2.0` (budgets multiplied).
- Perf classes run in one non-parallel xUnit collection (`PerfCollection`). Timed sections call
  `PerfHelpers.SettleGc()` first (full GC; on Windows it also opts the process out of EcoQoS so hybrid CPUs do not
  park the test on efficiency cores). ADR-052.
- SIM-P1 runs seed 1 with `SurvivalScript` to tick 12,000 and times 500 `Tick()` calls. It prints the water and
  region phase totals; during the day-5 drought, region rebuilds are most of the tick.
- CON-P1 (`MonumentPerfTests`) runs seed 1 with `MonumentScript` to tick 13,000 (the tower's middle courses) and
  times 500 `Tick()` calls against the SIM-P1 budget. It prints the Build poster's phase total
  (`TickPhase.BlockBuild`) and the place trial's exact checks and cut computes.

| ID | Budget |
|---|---|
| WAT-P1 | water step ≤ 4 ms at 20k active cells |
| WAT-P2 | settled river ≤ 3000 active cells after 1200 ticks |
| PTH-P1 | p95 A* (≈100-cell paths) ≤ 1.5 ms |
| PTH-P2 | region rebuild ≤ 25 ms |
| ECO-16 | moisture recompute ≤ 3 ms |
| SIM-P1 | full `Tick()` median ≤ 8 ms on seed 1 with 5 agents at day 5 |
| CON-P1 | full `Tick()` median ≤ 8 ms on seed 1 during the monument build (tick 13,000) |
| MESH-P1 | greedy mesh one 32³ surface chunk ≤ 6 ms |

## Screenshot presets

`overview` (whole map, 45° pitch), `river` (hub-to-river close-up), `hub` (colony close-up), `slice` (slice at
y=20 over the hill); these four are the default `SHOTS`. `farm` (close-up on the farm tiles, the hub when there are
none; M6-T5) is available with `SHOTS=...,farm`. For the survival session (M7-T7), `tunnel` (the hill tunnel and
its breach, sliced at y=18, from the east) and `reservoir` (the levee reservoir at x=67 and the farm beside it, from
the south over the river). For the monument session (M8-T6), `monument` (the tower and courtyard, from the
south-west, yaw 30°). For the single-block tool (M9-T1), `paint` (the `PaintScript` site near the hub). Output to `artifacts/screens/<preset>.png`. Human gates review them.

Shots render with Forward+ (`--rendering-method forward_plus` on the default Vulkan driver), the renderer the game
plays in, so they show the same lighting and colors as play (ADR-035). There is no separate Compatibility render.
On Linux under `xvfb-run` this needs a Vulkan driver (e.g. Mesa lavapipe). Env options: `SEED`, `TICKS` (1200),
`SHOTS`, `OUT`, and `SCRIPT=digchop` (dig a pit and chop trees near the hub, so colonists are at work;
`TICKS=400` shows the marks, 1200 shows the piles) or `SCRIPT=build` (chop plus a warehouse, a pump and a levee line
near the hub; `TICKS=200` catches them mid-build (500 before the M11-T2 wagon), 1600 shows them complete, ADR-048; add `GHOST=levee` for a green
levee ghost at the end of the line with no entrance tile, M11-T1, `SHOTS=hub`) or `SCRIPT=farm` (a 5×5 field on
the nearest moist ground to the hub; `TICKS=4000` shows growing crops, 9000 the first mature ones and harvests) or
`SCRIPT=survival` (the timed `SurvivalScript`, each command enqueued at its tick, M7-T7). Useful survival shots:
`TICKS=8000 SHOTS=tunnel` (flooded tunnel, breach levee sites), `TICKS=8700 SHOTS=tunnel` (levees complete),
`TICKS=3000 SHOTS=reservoir` (full reservoir), `TICKS=14400 SHOTS=reservoir,tunnel,river` (drought: the river is
empty, the reservoir and the tunnel still hold water). `SCRIPT=monument` (the timed `MonumentScript`, M8-T6):
`TICKS=11000 SHOTS=monument` (first courses and plan ghosts), 14000 (tower half built), 17400 (complete).
`SCRIPT=wall SHOTS=wall` (M10-T3) runs the same drags as `paint`, and the harness holds a vertical Wood planks drag
up a wall face beside the L instead (`TICKS=1500`: the built L, and the 4x6 ghost wall with its top row amber).
`SCRIPT=stairs SHOTS=stairs` (M11-T8): a stair-down dig to stone north-east of the hall with a quarry room at its
foot, and a straight-sided 3-cube pit beside it; the harness selects the dig tool in stair mode and hovers a waiting
pit cell (`TICKS=700`: digging, the pit's lower cells "would trap a dwarf"; 2000: stair and room dug).
`SCRIPT=paint` (the timed `PaintScript`, M9-T1): single blocks painted with the player's block tool along drags, a
planks L released at tick 0 and a planned second course at tick 1, plus a stone paint drag held by the harness;
`TICKS=3 SHOTS=paint` shows the ghosts, 1500 the built L with the planned course and a red cell where the held drag
crosses it. `SCRIPT=materials` (`MaterialsScript`, M9-T4): one 2x2 released sample of every construction block in
Blocks-menu order with a planned course of the same block on top, a pit dug 7 deep for the stone, and a Water Pump
at the nearest wet site (M10-T5); `TICKS=12000 SHOTS=materials` shows all six built, and all 5 dwarves live to day 10. The harness does not rebuild the C# assembly reliably:
run `dotnet build src/Aurvangar.Godot` after code changes, before `screenshot.sh`.

## Scripted play

`Scripts.SurvivalScript` (`Aurvangar.ViewCore.Scripts`, pure C#, ADR-045, ADR-051) is the command log for the
definition-of-done session on seed 1. It is a fixed list of `(tick, command)`; call
`SurvivalScript.EnqueueDue(sim)` before every `Tick()` (or use `SurvivalScript.Run`). It is used by the golden test,
the survival scenario (`SurvivalScenarioTests`), and `run-headless.sh --script survival`. Complete since M6-T6:

| Tick | Command |
|---|---|
| 0 | dig the reservoir's pump end `(67,17..18,70)` and the pump pad `(68,18,70)` (ADR-056) |
| 600 | pump at `(68,18,70)` rot 90, intake in the reservoir, raised stand (ADR-055); chop the 48×48 area around the Great Hall; dig the reservoir trench `(67,17..19,61..70)` |
| 1200 | dig the dam `(67,17,71)`: the river fills the reservoir; warehouse at `(34,24,54)` |
| 1800 | five levees `(33..37,23,76)` on the bank between the hall and the river |
| 2400 | 5×6 farm field `(62,67)..(66,72)` beside the reservoir |
| 3000 | tunnel into the hill's south slope `(84,17,56)..(85,18,65)`; its north half is stone |
| 7500 | breach (N, 7200 before the M11-T2 wagon): dig the bank cells `(84..85,17,66)` between the tunnel mouth and the river; the tunnel floods |
| 7800 | levee repair (N+300): a levee on each breach cell; the flood is walled off |
| 9600 | seal levee on the reservoir mouth `(67,17,71)`; the reservoir holds through the drought (M7-T3) |

`Scripts.MonumentScript` (M8-T6, ADR-066) is the monument session on seed 1: chop, a pump, a corridor and quarry
room dug into the hill's stone with two warehouses inside it, and a planned hollow 7×7, 8-high Masonry tower (door,
inner stair) plus a courtyard wall. The whole plan is released at tick 9600, as a player's "Release all" does
(M9-T2, ADR-068); the CON-05 course check and CON-12 batching raise it course by course in full trips, and the next
course starts where the one below is built (M10-T4, ADR-074: at most 15% idle dwarf time from the release to
completion, `Seed1_Monument_FewIdleBuildersBetweenCourses`). Used by `MonumentScenarioTests`, CON-P1 and
`run-headless.sh --script monument`.

`run-headless.sh --script digchop` (also `none`, `build`, `farm`, `survival` and `monument`) runs the screenshot harness's `ScreenshotScripts` command list
(ViewCore, Godot-free) so the same dig + chop work can be measured without Godot (ADR-036). With a script it adds a
`work:` line (marks left, piles, stored items, path searches, trapped agents) and a `colony:` line (season, complete
buildings by type, pumps flagged NoWater, farm tiles and crop states, storage by item) per report, and a final
`summary:` (jobs completed/failed, cells dug, trees felled, net change in storage, crops harvested and withered, pump
NoWater ticks by season). Dig, chop and crop counts are observed after every tick, so they include designations a
timed script (`survival`) adds after tick 1 (ADR-053).
