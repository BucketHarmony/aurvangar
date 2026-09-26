# Progress log

Newest entries at the bottom. One entry per backlog task, plus milestone reports and gate reports.

Entry template:

```
## <task id> — <title> (<date>)
- Done: <what changed, 1-3 lines>
- Tests: <tests un-skipped / added>
- Decisions: <ADR ids or "none">
- Golden: <"unchanged" or "regenerated: <why>">
- Perf: <numbers if touched, else "n/a">
- Next: <anything the next task must know>
```

---

## SCAFFOLD — initial repository (2026-09-25)
- Done: docs, specs, backlog, data files, solution, Aurvangar.Sim foundation (Core, ContentDb, VoxelWorld, Simulation
  loop, commands, events, state hash), scaffolds with typed stubs for water, pathing, agents, actions, buildings,
  save. ViewCore mesher stubs. Headless runner. Godot project shell with fixed-tick GameRoot. Scripts, hooks,
  agents, CI.
- Tests: 35 foundation tests pass. 124 acceptance tests are skipped with their backlog task id.
- Verified in the scaffolding environment: Aurvangar.Sim, Aurvangar.ViewCore and Aurvangar.Headless build with .NET 8.0.131;
  the test sources compile and the non-skipped tests pass under a local xUnit stand-in. NOT verified there (NuGet
  was unreachable): the real xUnit run and the Godot.NET.Sdk build. M0-T1 verifies both.
- Next: M0-T1.

## M0-T1 — Verify toolchain and repo (2026-09-26)
- Done: Confirmed `git status` clean on `main` before starting. `./scripts/check.sh` green: sim-guard OK, solution
  builds with 0 warnings, Godot csproj (`Godot.NET.Sdk/4.6.2`, restored from NuGet) builds. No code changes.
- Tool versions: Windows 11 (Git Bash), git 2.50.1.windows.1; .NET SDKs 6.0.428, 8.0.425, 9.0.312, 10.0.201
  (default `dotnet` 10.0.201, all projects target net8.0); xunit 2.9.2, xunit.runner.visualstudio 2.8.2,
  Microsoft.NET.Test.Sdk 17.11.1. Godot: winget `GodotEngine.GodotEngine` 4.6.1.stable.official — the standard
  (non-.NET) edition, so it cannot build/run C# projects. `GODOT_BIN` is unset; the
  `--headless --build-solutions` step was skipped per CLAUDE.md.
- Tests: 35 passed, 118 skipped, 0 failed (non-Perf). Plus 6 skipped Perf tests = 124 acceptance skips, matching
  the scaffold count.
- Decisions: none
- Golden: unchanged
- Perf: n/a
- Next: M0-T2 (CI). Screenshot/editor tasks (M3-T6 onward) need a Godot 4.6 .NET (mono) binary in `GODOT_BIN`;
  the installed winget build is not the .NET edition.

## M0-T2 — CI (2026-09-26)
- Done: No code changes. `act` is not installed, so verified by inspection plus real GitHub runs:
  `.github/workflows/ci.yml` job `check` runs `./scripts/check.sh` verbatim (checkout + setup-dotnet 8.0.x), so it
  mirrors the local gate exactly (sim-guard, sln build, non-Perf tests, Godot csproj build). Job `perf` runs
  `./scripts/perf.sh` with `PERF_SCALE=2.0` and `continue-on-error: true` (non-blocking); PerfHelpers reads
  `PERF_SCALE`. Scripts are committed as mode 100755 and LF (.gitattributes), so they run on ubuntu.
  `gh run list`: last 5 `ci` runs on main all succeeded (e.g. run 36253357192: check 21s, perf 27s).
  Local `./scripts/check.sh` green; local `./scripts/perf.sh` runs (6 Perf tests, all skipped).
- Tests: 35 passed, 118 skipped, 0 failed (non-Perf); 6 Perf skipped. None un-skipped (task has no tests).
- Decisions: none
- Golden: unchanged
- Perf: n/a
- Next: M1-T1/M1-T2 are done; next is M1-T3 (terrain generator). Follow-up, not blocking: GitHub warns that
  actions/checkout@v4 and actions/setup-dotnet@v4 target deprecated Node 20, and ubuntu-latest moves to Ubuntu 26
  from 2026-10-19; bump to v5 actions / pin the runner when convenient.
