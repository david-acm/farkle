#!/usr/bin/env bash
# Reproduces the CI coverage gate locally, with the same scripts CI runs: collect across the
# device-free suites, write an HTML report, then run the new-code gate against a base branch.
#
# The point is that a surprise in CI should be reproducible here in one command — if this passes
# and CI's gate fails, that is a bug in the setup, not in your change.
#
# Usage: tests/scripts/coverage.sh [base-branch]   (default: origin/main)
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/../.."

BASE="${1:-origin/main}"
COV="$(mktemp -d)"
RUNSETTINGS="$PWD/coverage.runsettings"
echo "coverage: collecting to $COV (base: $BASE)"

collect() {
  dotnet test "$1" \
    --collect:"XPlat Code Coverage" \
    --settings "$RUNSETTINGS" \
    --results-directory "$COV" \
    --nologo -v q "${@:2}"
}

# The device-free tiers — no emulator, no device, no MAUI workload. HotDice.WebTests needs Docker
# (Testcontainers/Postgres); pass --fast to skip it when you only touched client/domain code.
collect tests/HotDice.Tests/HotDice.Tests.csproj
collect tests/HotDice.ArchitectureTests/HotDice.ArchitectureTests.csproj
collect tests/HotDice.Client.Tests/HotDice.Client.Tests.csproj
collect tests/HotDice.SpaTests/HotDice.SpaTests.csproj
collect tests/Blazor.Dice.Tests/Blazor.Dice.Tests.csproj
if [ "${FAST:-}" != "1" ]; then
  collect tests/HotDice.WebTests/HotDice.WebTests.csproj
else
  echo "coverage: FAST=1 — skipping HotDice.WebTests (Docker). The gate may flag server lines it covers."
fi

bash tests/scripts/coverage-html.sh "$COV" coverage-html
echo
bash tests/scripts/diff-coverage.sh "$BASE" "$COV" "${FAIL_UNDER:-100}"
