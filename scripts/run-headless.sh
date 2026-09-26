#!/usr/bin/env bash
# Headless sim run. Example: ./scripts/run-headless.sh --seed 1 --ticks 24000 --report-every 2400
set -euo pipefail
cd "$(dirname "$0")/.."
dotnet run --project tools/Colony.Headless -c Release -- "$@"
