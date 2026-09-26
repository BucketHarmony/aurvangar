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
y=20 over the hill). Output to `artifacts/screens/<preset>.png`. Human gates review them.

Shots render with Forward+ (`--rendering-method forward_plus` on the default Vulkan driver), the renderer the game
plays in, so they show the same lighting and colors as play (ADR-035). There is no separate Compatibility render.
On Linux under `xvfb-run` this needs a Vulkan driver (e.g. Mesa lavapipe). Env options: `SEED`, `TICKS` (1200),
`SHOTS`, `OUT`, and `SCRIPT=digchop` (dig a pit and chop trees near the hub, so colonists are at work;
`TICKS=400` shows the marks, 1200 shows the piles).

## Scripted play

`Scripts.SurvivalScript` (in the test project) is the command log for the definition-of-done session: place
pump, farm 6×6 field, chop area, place warehouse, dig into the hill, breach the bank at tick N, levee line at
tick N+300. It is used by the golden test, the survival scenario, and `run-headless.sh --script survival`.
Build it incrementally: each milestone that adds a command type extends the script.

`run-headless.sh --script digchop` (also `none`) runs the screenshot harness's `ScreenshotScripts` command list
(ViewCore, Godot-free) so the same dig + chop work can be measured without Godot (ADR-036). With a script it adds a
`work:` line per report (marks left, piles, stored items, path searches, trapped agents) and a final `summary:`
(jobs completed/failed, cells dug, trees felled, items hauled into storage).
