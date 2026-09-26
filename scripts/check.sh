#!/usr/bin/env bash
# Full gate: sim guard, build, all non-perf tests, Godot project build. Must pass before every commit.
set -euo pipefail
cd "$(dirname "$0")/.."

echo "== sim guard"
./scripts/sim-guard.sh

echo "== build"
dotnet build Aurvangar.sln -c Debug -nologo -v q

echo "== tests (excluding Perf)"
dotnet test tests/Aurvangar.Sim.Tests -c Debug --no-build -nologo \
  --filter "Category!=Perf" --logger "console;verbosity=minimal"

echo "== godot project build"
if dotnet build src/Aurvangar.Godot/Aurvangar.Godot.csproj -c Debug -nologo -v q; then
  :
else
  echo "Godot project failed to build." >&2
  exit 1
fi

echo "== check.sh: OK"
