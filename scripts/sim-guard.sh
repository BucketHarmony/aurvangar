#!/usr/bin/env bash
# Enforces the sim/view boundary and determinism rules from CLAUDE.md with plain greps.
# Exit 2 with a message on violation (exit 2 also makes it usable as a blocking Claude Code hook).
set -uo pipefail
cd "$(dirname "$0")/.."

fail=0
report() { echo "sim-guard: $1" >&2; fail=1; }
# grep that ignores comment lines (//, ///, *)
code_grep() { grep -nE "$1" "${@:2}" | grep -vE '^[^:]+:[0-9]+:[[:space:]]*(//|\*)'; }

# 1. No Godot in the sim or ViewCore.
if grep -rnE '^\s*using\s+Godot|Godot\.' --include='*.cs' src/Aurvangar.Sim src/Aurvangar.ViewCore; then
  report "Godot referenced from Aurvangar.Sim or Aurvangar.ViewCore (hard rule: sim/view boundary)"
fi

# 2. Nondeterministic or floating-point APIs in the sim. Content/Defs.cs holds view-only palette floats and is exempt.
SIM_FILES=$(find src/Aurvangar.Sim -name '*.cs' -not -path '*/obj/*' -not -path '*/bin/*' -not -name 'Defs.cs')
if [ -n "$SIM_FILES" ]; then
  if code_grep '\bnew Random\b|System\.Random|DateTime|Guid\.|Environment\.TickCount|Math\.(Sin|Cos|Tan|Sqrt|Pow|Exp|Log)\b|MathF\.' $SIM_FILES; then
    report "nondeterministic or floating-point API in Aurvangar.Sim (use Core/Rng, Core/Fixed)"
  fi
  if code_grep '\b(float|double|decimal)\b' $SIM_FILES; then
    report "float/double/decimal in Aurvangar.Sim state or logic (integers only; see docs/02-conventions.md)"
  fi
  if code_grep '\b(Thread|Task\.Run|Parallel\.)' $SIM_FILES; then
    report "threading in Aurvangar.Sim (ARCH-05: single-threaded sim)"
  fi
fi

# 3. No GDScript anywhere.
if find . -name '*.gd' -not -path './.git/*' | grep -q .; then
  report "GDScript file found (C# only)"
fi

if [ "$fail" -ne 0 ]; then exit 2; fi
echo "sim-guard: OK"
