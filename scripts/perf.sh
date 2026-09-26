#!/usr/bin/env bash
# Perf budgets (docs/testing.md). Release build. Set PERF_SCALE=2.0 on slow machines.
set -euo pipefail
cd "$(dirname "$0")/.."
dotnet build Aurvangar.sln -c Release -nologo -v q
dotnet test tests/Aurvangar.Sim.Tests -c Release --no-build -nologo \
  --filter "Category=Perf" --logger "console;verbosity=normal"
