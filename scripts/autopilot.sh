#!/usr/bin/env bash
# Runs Claude Code headless in a loop, one backlog task per iteration, until a HUMAN-GATE, BLOCKED stop,
# or the iteration cap. Each iteration is a fresh session; state lives in BACKLOG.md, PROGRESS.md and git.
#
#   ./scripts/autopilot.sh            # up to 40 iterations
#   MAX_ITER=10 MODEL=opus ./scripts/autopilot.sh
set -uo pipefail
cd "$(dirname "$0")/.."

MAX_ITER="${MAX_ITER:-40}"
MAX_TURNS="${MAX_TURNS:-150}"
MODEL_ARGS=()
[ -n "${MODEL:-}" ] && MODEL_ARGS=(--model "$MODEL")
mkdir -p artifacts/autopilot

PROMPT='Run exactly one iteration of the work loop in CLAUDE.md (the /next-task procedure): pick the next task from BACKLOG.md, implement it test-first, make ./scripts/check.sh green, update BACKLOG.md and PROGRESS.md, and commit. If the next task is a HUMAN-GATE, write the gate report and end your reply with the line AUTOPILOT: GATE. If you are blocked per CLAUDE.md, end with AUTOPILOT: BLOCKED. If the backlog is empty, end with AUTOPILOT: DONE. Otherwise end with AUTOPILOT: CONTINUE.'

for i in $(seq 1 "$MAX_ITER"); do
  log="artifacts/autopilot/iter-$(printf %03d "$i").log"
  echo "== autopilot iteration $i -> $log"
  claude -p "$PROMPT" --permission-mode acceptEdits --max-turns "$MAX_TURNS" "${MODEL_ARGS[@]}" > "$log" 2>&1
  status=$(grep -oE 'AUTOPILOT: (GATE|BLOCKED|DONE|CONTINUE)' "$log" | tail -1 || true)
  echo "   $status"
  case "$status" in
    "AUTOPILOT: CONTINUE") continue ;;
    "AUTOPILOT: GATE"|"AUTOPILOT: DONE") exit 0 ;;
    *) echo "Stopping: $status (see $log)"; exit 1 ;;
  esac
done
echo "Iteration cap reached."
