#!/usr/bin/env bash
# Renders preset camera shots to artifacts/screens/<preset>.png (VIEW-20, docs/testing.md "Screenshot presets").
# Needs GODOT_BIN = the Godot 4.6 .NET (mono) editor binary. Works on Linux (uses xvfb-run when there is no DISPLAY)
# and on Windows Git Bash (runs Godot directly; prefer the *_console.exe binary to see its output).
# Env overrides: SEED (1), TICKS (1200), SHOTS (overview,river,hub,slice), OUT (artifacts/screens).
set -euo pipefail
cd "$(dirname "$0")/.."

die() { echo "screenshot.sh: $*" >&2; exit 1; }

if [ -z "${GODOT_BIN:-}" ]; then
  die "GODOT_BIN is not set. Point it at the Godot 4.6 .NET (mono) editor binary, e.g.
  export GODOT_BIN=/opt/godot/Godot_v4.6-stable_mono_linux.x86_64
  export GODOT_BIN='C:/Tools/Godot/Godot_v4.6-stable_mono_win64_console.exe'
Screenshots were NOT produced."
fi
if ! command -v "$GODOT_BIN" >/dev/null 2>&1 && [ ! -x "$GODOT_BIN" ]; then
  die "GODOT_BIN='$GODOT_BIN' is not an executable file. Screenshots were NOT produced."
fi

VERSION="$("$GODOT_BIN" --headless --version 2>/dev/null | tr -d '\r' | tail -n 1 || true)"
case "$VERSION" in
  4.6*mono*) ;;
  4.6*) die "GODOT_BIN is Godot '$VERSION', the standard edition. The C# project needs the .NET (mono) edition." ;;
  *) die "GODOT_BIN reports version '$VERSION'; expected Godot 4.6 .NET (mono)." ;;
esac

# Godot on Windows cannot read MSYS paths like /e/ai/...; `pwd -W` gives E:/ai/... under Git Bash.
if pwd -W >/dev/null 2>&1; then ROOT="$(pwd -W)"; else ROOT="$PWD"; fi
OUT="${OUT:-$ROOT/artifacts/screens}"
SHOTS="${SHOTS:-overview,river,hub,slice}"
mkdir -p "$OUT"
IFS=',' read -r -a SHOT_LIST <<< "$SHOTS"
for s in "${SHOT_LIST[@]}"; do rm -f "$OUT/$s.png"; done

echo "== building C# solution (Godot $VERSION)"
"$GODOT_BIN" --headless --path "$ROOT/src/Aurvangar.Godot" --build-solutions --quit

ARGS=(--path "$ROOT/src/Aurvangar.Godot" --rendering-driver opengl3 res://scenes/Screenshot.tscn
      -- --seed "${SEED:-1}" --ticks "${TICKS:-1200}" --shots "$SHOTS" --out "$OUT")
echo "== rendering $SHOTS"
if [ "$(uname -s)" = "Linux" ] && [ -z "${DISPLAY:-}" ]; then
  command -v xvfb-run >/dev/null || die "no DISPLAY and xvfb-run is not installed (Godot needs a window to render)."
  xvfb-run -a -s "-screen 0 1600x900x24" "$GODOT_BIN" "${ARGS[@]}"
else
  "$GODOT_BIN" "${ARGS[@]}"
fi

missing=0
for s in "${SHOT_LIST[@]}"; do
  if [ -s "$OUT/$s.png" ]; then echo "$OUT/$s.png"; else echo "missing: $OUT/$s.png" >&2; missing=1; fi
done
[ "$missing" -eq 0 ] || die "some screenshots were not produced."
