#!/usr/bin/env bash
# PostToolUse hook: after any edit under src/, run the sim guard. Exit 2 shows the violation to Claude.
input=$(cat)
file=$(printf '%s' "$input" | jq -r '.tool_input.file_path // empty')
case "$file" in
  */src/Colony.Sim/*|*/src/Colony.ViewCore/*|*.gd)
    cd "${CLAUDE_PROJECT_DIR:-.}" || exit 0
    out=$(./scripts/sim-guard.sh 2>&1) || { echo "$out" >&2; exit 2; }
    ;;
esac
exit 0
