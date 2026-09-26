#!/usr/bin/env bash
# Renders preset camera shots to artifacts/screens (VIEW-20). Needs GODOT_BIN (Godot 4.6 .NET) and, headless, xvfb-run.
set -euo pipefail
cd "$(dirname "$0")/.."
: "${GODOT_BIN:?Set GODOT_BIN to the Godot 4.6 .NET editor binary}"
OUT="$PWD/artifacts/screens"
mkdir -p "$OUT"
ARGS=(--path src/Aurvangar.Godot --rendering-driver opengl3 res://scenes/Screenshot.tscn
      -- --seed "${SEED:-1}" --ticks "${TICKS:-1200}" --shots "${SHOTS:-overview,river,hub,slice}" --out "$OUT")
"$GODOT_BIN" --headless --path src/Aurvangar.Godot --build-solutions --quit
if command -v xvfb-run >/dev/null && [ -z "${DISPLAY:-}" ]; then
  xvfb-run -a -s "-screen 0 1600x900x24" "$GODOT_BIN" "${ARGS[@]}"
else
  "$GODOT_BIN" "${ARGS[@]}"
fi
ls -1 "$OUT"
