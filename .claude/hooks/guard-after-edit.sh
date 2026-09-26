#!/usr/bin/env bash
# PostToolUse hook: after any edit under the sim or view-core sources, run the sim guard.
# Exit 2 shows the violation to Claude. No jq dependency (plain grep/sed) so it works in stock Git Bash.
input=$(cat)
file=$(printf '%s' "$input" | tr -d '\r' | grep -o '"file_path"[[:space:]]*:[[:space:]]*"[^"]*"' | head -1 | sed 's/.*:[[:space:]]*"//; s/"$//')
file=${file//\\\\//}   # JSON-escaped Windows backslashes -> forward slashes
file=${file//\\//}
case "$file" in
  */src/Aurvangar.Sim/*|*/src/Aurvangar.ViewCore/*|*.gd)
    cd "${CLAUDE_PROJECT_DIR:-.}" || exit 0
    out=$(bash ./scripts/sim-guard.sh 2>&1) || { echo "$out" >&2; exit 2; }
    ;;
esac
exit 0
