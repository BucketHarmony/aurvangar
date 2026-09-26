---
description: Work the backlog task by task (one fresh subagent per task) until the next HUMAN-GATE report is committed.
argument-hint: "[extra notes for every task subagent]"
---

Work through BACKLOG.md using the work loop in CLAUDE.md, acting as the orchestrator. Do not implement tasks
yourself.

## Loop

1. Run `git status -sb` and `git log -1 --oneline`. If the working tree is dirty, stop and report it. Find the first
   unchecked task in BACKLOG.md whose deps are all checked, and name the next HUMAN-GATE that this run will stop at.
2. Launch ONE fresh `general-purpose` subagent with the task prompt below, and wait for it to finish. Never run two
   task subagents at once, because they share the working tree.
3. When it finishes, verify:
   - `git log -1 --oneline` shows a new commit for that task;
   - `git status --short` is clean;
   - `./scripts/check.sh` is green.

   If a check fails, send the same subagent a message to fix it, one retry at most. If it is still red, stop and
   report.
4. Give the user one short line with the task, its commit and the test counts. Then go to step 1.

## Stop when any of these is true

- The next HUMAN-GATE gate report is committed to PROGRESS.md. As proof, show `git log -1 --oneline` and the last
  30 lines of PROGRESS.md, then summarize the gate's questions for the human.
- A task is marked BLOCKED and no remaining task is unblocked.
- 50 tasks have run in this invocation.

Don't push; `git push` is denied in this repo. At the end, remind the user to run `! git push`.

## Task subagent prompt (pass verbatim, then append $ARGUMENTS if any)

> You are working in the repo at E:\ai\aurvangar (Windows 11; use the Bash tool, which is Git Bash, for
> ./scripts/*.sh). It's a Godot 4.6 .NET / C# voxel colony sim. Read CLAUDE.md fully first; it is the operating
> manual.
>
> Run exactly ONE iteration of the work loop, following .claude/commands/next-task.md:
> 1. Read the last 3 entries of PROGRESS.md and all of BACKLOG.md. Pick the first unchecked task whose deps are
>    all checked. State its ID and title.
> 2. If it is a HUMAN-GATE task, write the gate report it asks for in PROGRESS.md, commit, and stop. Do NOT check
>    the gate box. Include real screenshots (render them and look at them) and the results of check.sh, perf.sh and
>    a headless run.
> 3. Read every spec ID it references and the code it touches. Also read the human notes on the task in BACKLOG.md
>    and any "G<n> answers" or "G<n> follow-up" sections in PROGRESS.md. Human decisions override specs.
> 4. Un-skip or write the acceptance tests first. Run them and confirm they fail for the right reason.
> 5. Implement until ./scripts/check.sh is fully green. For perf tasks, or when touching water or paths, also run
>    ./scripts/perf.sh. Never weaken, skip or delete tests; never loosen a perf budget. If a spec is ambiguous, pick
>    the simplest option consistent with docs/00-overview.md, record an ADR in docs/decisions.md, and continue.
> 6. If the change to src/Aurvangar.Sim is over ~150 lines, run the sim-reviewer agent and apply its required fixes.
>    If a perf budget fails or is within 20% of its limit, you may use the perf-auditor agent.
> 7. Check the box in BACKLOG.md, append a PROGRESS.md entry, and commit as "<ID>: <title>" with
>    `git add -A && git commit`, ending the message with the Co-Authored-By trailer from your system instructions.
>
> Environment:
> - git push is denied; only commit. Don't amend, rebase or reset --hard.
> - Godot .NET 4.6.2 is at /c/tools/godot/Godot_v4.6.2-stable_mono_win64_console.exe. Always run Godot scripts with
>   `GODOT_BIN="${GODOT_BIN:-/c/tools/godot/Godot_v4.6.2-stable_mono_win64_console.exe}"`.
>   `./scripts/screenshot.sh` writes artifacts/screens/*.png; view them with the Read tool. For any task that
>   touches rendering or Godot-side code, render screenshots and look at them before committing.
> - Do NOT download or install software.
> - Edit/Write under tests/golden/* is denied. Regenerate goldens only with
>   `UPDATE_GOLDEN=1 dotnet test tests/Aurvangar.Sim.Tests --filter Category=Golden`, and say why in PROGRESS.md.
> - Wait for any sub-agents you launch to finish before committing.
> - On the CLAUDE.md "When to stop" conditions, record BLOCKED per CLAUDE.md, commit, and stop.
>
> Do only one task. Final report (short): task ID and title, commit hash, check.sh and perf.sh status, test counts,
> key numbers, ADRs, anything BLOCKED, and anything the next task must know.
