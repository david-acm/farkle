#!/usr/bin/env bash
# New-code coverage gate: fails when a line you added or changed, in a file the tests actually
# cover, is untested. Runs diff-cover over the Cobertura reports from
# `dotnet test --collect:"XPlat Code Coverage"`.
#
# The scope is set by construction, not by a list here: a file absent from every coverage report
# (the MAUI shell, generated code excluded in coverage.runsettings) is not counted. So the gate
# targets code the fast suite genuinely exercises, and never blocks a change it could not have
# tested in the first place.
#
# Usage: diff-coverage.sh <base-branch> <coverage-root-dir> [fail-under] [markdown-out]
set -euo pipefail

BASE="${1:-origin/main}"
COV_ROOT="${2:-.}"
FAIL_UNDER="${3:-100}"
MD_OUT="${4:-}"

python3 -m pip install --quiet --user --disable-pip-version-check diff-cover >/dev/null

# macOS ships bash 3.2, which has no `mapfile`.
reports=()
while IFS= read -r f; do reports+=("$f"); done < <(find "$COV_ROOT" -name 'coverage.cobertura.xml' 2>/dev/null)

if [ "${#reports[@]}" -eq 0 ]; then
  echo "diff-coverage: no coverage.cobertura.xml under '$COV_ROOT' — did the tests run with --collect:\"XPlat Code Coverage\"?" >&2
  exit 1
fi

echo "diff-coverage: ${#reports[@]} report(s) vs '$BASE', fail-under=${FAIL_UNDER}%"

args=(--compare-branch "$BASE" --fail-under "$FAIL_UNDER")
[ -n "$MD_OUT" ] && args+=(--markdown-report "$MD_OUT")

python3 -m diff_cover.diff_cover_tool "${reports[@]}" "${args[@]}"
