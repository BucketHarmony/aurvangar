#!/usr/bin/env bash
# Stop hook: Claude may not end a turn with a red build.
# - Skipped when COLONY_SKIP_STOP_GATE=1 (interactive exploration) or when nothing under src/ or tests/ changed.
# - Never blocks twice in a row (stop_hook_active) so it cannot loop forever.
input=$(cat)
[ "${COLONY_SKIP_STOP_GATE:-0}" = "1" ] && exit 0
[ "$(printf '%s' "$input" | jq -r '.stop_hook_active // false')" = "true" ] && exit 0
cd "${CLAUDE_PROJECT_DIR:-.}" || exit 0
if git rev-parse --git-dir >/dev/null 2>&1; then
  if git diff --quiet HEAD -- src tests data 2>/dev/null && [ -z "$(git ls-files --others --exclude-standard src tests data)" ]; then
    exit 0
  fi
fi
if out=$(./scripts/check.sh 2>&1); then
  exit 0
fi
{
  echo "check.sh is red. Fix it before stopping (or record BLOCKED per CLAUDE.md). Last 60 lines:"
  printf '%s\n' "$out" | tail -60
} >&2
exit 2
