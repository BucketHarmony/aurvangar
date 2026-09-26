#!/usr/bin/env bash
# Stop hook: Claude may not end a turn with a red build.
# - Skipped when AURVANGAR_SKIP_STOP_GATE=1 (interactive exploration) or when nothing under src/, tests/ or data/ changed.
# - Never blocks twice in a row (stop_hook_active) so it cannot loop. No jq dependency.
input=$(cat)
[ "${AURVANGAR_SKIP_STOP_GATE:-0}" = "1" ] && exit 0
if printf '%s' "$input" | grep -q '"stop_hook_active"[[:space:]]*:[[:space:]]*true'; then
  exit 0
fi
cd "${CLAUDE_PROJECT_DIR:-.}" || exit 0
if git rev-parse --git-dir >/dev/null 2>&1; then
  if git diff --quiet HEAD -- src tests data 2>/dev/null && [ -z "$(git ls-files --others --exclude-standard src tests data)" ]; then
    exit 0
  fi
fi
if out=$(bash ./scripts/check.sh 2>&1); then
  exit 0
fi
{
  echo "check.sh is red. Fix it before stopping (or record BLOCKED per CLAUDE.md). Last 60 lines:"
  printf '%s\n' "$out" | tail -60
} >&2
exit 2
