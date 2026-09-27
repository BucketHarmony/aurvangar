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

| ID | Budget |
|---|---|
| WAT-P1 | water step ≤ 4 ms at 20k active cells |
| WAT-P2 | settled river ≤ 3000 active cells after 1200 ticks |
| PTH-P1 | p95 A* (≈100-cell paths) ≤ 1.5 ms |
| PTH-P2 | region rebuild ≤ 25 ms |
| ECO-16 | moisture recompute ≤ 3 ms |
| SIM-P1 | full `Tick()` median ≤ 8 ms on seed 1 with 5 agents at day 5 |
| MESH-P1 | greedy mesh one 32³ surface chunk ≤ 6 ms |

## Screenshot presets

`overview` (whole map, 45° pitch), `river` (hub-to-river close-up), `hub` (colony close-up), `slice` (slice at
y=20 over the hill); these four are the default `SHOTS`. `farm` (close-up on the farm tiles, the hub when there are
none; M6-T5) is available with `SHOTS=...,farm`. Output to `artifacts/screens/<preset>.png`. Human gates review them.

Shots render with Forward+ (`--rendering-method forward_plus` on the default Vulkan driver), the renderer the game
plays in, so they show the same lighting and colors as play (ADR-035). There is no separate Compatibility render.
On Linux under `xvfb-run` this needs a Vulkan driver (e.g. Mesa lavapipe). Env options: `SEED`, `TICKS` (1200),
`SHOTS`, `OUT`, and `SCRIPT=digchop` (dig a pit and chop trees near the hub, so colonists are at work;
`TICKS=400` shows the marks, 1200 shows the piles) or `SCRIPT=build` (chop plus a warehouse, a pump and a levee line
near the hub; `TICKS=500` catches them mid-build, 1600 shows them complete, ADR-048) or `SCRIPT=farm` (a 5×5 field on
the nearest moist ground to the hub; `TICKS=4000` shows growing crops, 9000 the first mature ones and harvests).

## Scripted play

`Scripts.SurvivalScript` (`Aurvangar.ViewCore.Scripts`, pure C#, ADR-045, ADR-051) is the command log for the
definition-of-done session on seed 1. It is a fixed list of `(tick, command)`; call
`SurvivalScript.EnqueueDue(sim)` before every `Tick()` (or use `SurvivalScript.Run`). It is used by the golden test,
the survival scenario (`SurvivalScenarioTests`), and `run-headless.sh --script survival`. Complete since M6-T6:

| Tick | Command |
|---|---|
| 0 | dig the pump-entrance notch `(40,18,79)` |
| 600 | pump at `(40,18,80)`; chop the 48×48 area around the Great Hall |
| 1200 | warehouse at `(34,24,54)` |
| 1800 | five levees `(33..37,23,76)` on the bank between the hall and the river |
| 2400 | 6×6 farm field `(62,67)..(67,72)` on moist ground |
| 3000 | tunnel into the hill's south slope `(84,17,56)..(85,18,65)`; its north half is stone |
| 7200 | breach (N): dig the bank cells `(84..85,17,66)` between the tunnel mouth and the river; the tunnel floods |
| 7500 | levee repair (N+300): a levee on each breach cell; the flood is walled off |

`run-headless.sh --script digchop` (also `none`, `build`, `farm` and `survival`) runs the screenshot harness's `ScreenshotScripts` command list
(ViewCore, Godot-free) so the same dig + chop work can be measured without Godot (ADR-036). With a script it adds a
`work:` line (marks left, piles, stored items, path searches, trapped agents) and a `colony:` line (season, complete
buildings by type, pumps flagged NoWater, farm tiles and crop states, storage by item) per report, and a final
`summary:` (jobs completed/failed, cells dug, trees felled, net change in storage, crops harvested and withered, pump
NoWater ticks by season). Dig, chop and crop counts are observed after every tick, so they include designations a
timed script (`survival`) adds after tick 1 (ADR-053).
