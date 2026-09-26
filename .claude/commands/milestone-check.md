---
description: Verify a milestone's exit criteria and write the milestone report to PROGRESS.md.
argument-hint: <milestone, e.g. M2>
---

For milestone $ARGUMENTS:

1. Confirm every task in that milestone section of BACKLOG.md is checked (except its HUMAN-GATE).
2. Run ./scripts/check.sh and ./scripts/perf.sh. Record results.
3. Grep the tests for `Skip = "$ARGUMENTS-` — none may remain for this milestone.
4. Run ./scripts/run-headless.sh --seed 1 --ticks 6000 --report-every 1200 and include the output summary.
5. If GODOT_BIN is set, run ./scripts/screenshot.sh and list the files.
6. Append a "Milestone $ARGUMENTS report" to PROGRESS.md: tasks done, perf table vs budgets, ADRs added, known
   issues, and anything the next milestone needs to know.
