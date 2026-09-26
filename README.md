# Colony Sim (working title)

Voxel colony builder POC in the Timberborn / Dwarf Fortress vein. Godot 4.6 .NET, C# only, deterministic
simulation in a Godot-free library. Built largely by Claude Code working through `BACKLOG.md`.

## Layout

```
CLAUDE.md                 operating manual for Claude Code (read first)
BACKLOG.md                milestone task queue with acceptance tests
PROGRESS.md               per-task log, milestone and gate reports
docs/                     overview, architecture, conventions, testing, decisions (ADRs), specs/
data/                     blocks, items, buildings, palette (embedded into Colony.Sim)
src/Colony.Sim            simulation (no Godot)
src/Colony.ViewCore       engine-neutral meshing and view logic (no Godot)
src/Colony.Godot          Godot project (namespace Colony.Client)
tests/Colony.Sim.Tests    xUnit: unit, scenario, golden, perf
tools/Colony.Headless     console runner
scripts/                  check, perf, run-headless, screenshot, autopilot, sim-guard
.claude/                  settings (permissions, hooks), agents, commands
```

## Requirements

- .NET 8 SDK
- Godot 4.6 .NET edition (for running the game and screenshots). Set `GODOT_BIN` to its binary.
- Claude Code
- `jq` (used by the Claude Code hooks), `xvfb-run` for headless screenshots on Linux

## Running the build with Claude Code

Interactive, one task at a time:

```bash
claude
> /next-task
```

Unattended, until the next human gate:

```bash
./scripts/autopilot.sh
```

The autopilot runs one fresh `claude -p` session per backlog task and stops at `HUMAN-GATE` tasks (after M3,
M4 and M6), when blocked, or after `MAX_ITER` iterations. Logs go to `artifacts/autopilot/`. Review the gate
report at the bottom of `PROGRESS.md`, answer its questions (edit specs or add backlog tasks), check the gate
box in `BACKLOG.md`, and start it again.

The Stop hook runs `./scripts/check.sh` before Claude can end a turn with source changes. Set
`COLONY_SKIP_STOP_GATE=1` for exploratory interactive sessions.

## Manual commands

```bash
./scripts/check.sh
./scripts/perf.sh
./scripts/run-headless.sh --seed 1 --ticks 24000 --report-every 2400
GODOT_BIN=/path/to/Godot_v4.6-stable_mono ./scripts/screenshot.sh
$GODOT_BIN --path src/Colony.Godot            # play
```
