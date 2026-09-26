---
description: Run one iteration of the CLAUDE.md work loop (next backlog task, test-first, green, commit).
---

Follow "The work loop" in CLAUDE.md for exactly one task:

1. Read the last 3 entries of PROGRESS.md and all of BACKLOG.md. Identify the first unchecked task whose deps are
   checked. State its ID and title in one line.
2. If it is a HUMAN-GATE task, write the gate report it asks for in PROGRESS.md, commit, and stop.
3. Read every spec ID it references and the code it touches.
4. Un-skip (and write bodies for placeholder) acceptance tests. Run them; confirm they fail for the right reason.
5. Implement. Run ./scripts/check.sh until green. Do not weaken tests.
6. If the change to src/Aurvangar.Sim is over ~150 lines, run the sim-reviewer agent and apply its required fixes.
7. Check the box in BACKLOG.md, append the PROGRESS.md entry, commit as "<ID>: <title>".

$ARGUMENTS
