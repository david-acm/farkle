#!/usr/bin/env bash
#
# Smoke tests for .github/scripts/generate-pages.sh.
# Run locally with:  bash tests/scripts/generate-pages.test.sh
#
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
GEN="$SCRIPT_DIR/.github/scripts/generate-pages.sh"
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT

PAGES="$WORK/pages"
mkdir -p "$PAGES"

fail=0
check() { # check <description> <condition-result>
  if [ "$2" = "0" ]; then
    echo "  ✓ $1"
  else
    echo "  ✗ $1"; fail=1
  fi
}

echo "Test 1: a passing run with two videos"
vids="$WORK/v1"; mkdir -p "$vids"
printf 'x' > "$vids/HappyPath.webm"
printf 'x' > "$vids/HappyPath-Bob.webm"
PAGES_DIR="$PAGES" VIDEOS_DIR="$vids" RUN_ID=1001 PR_NUMBER=42 \
  BRANCH=feature/x COMMIT_SHA=abcdef1234567 STATUS=success \
  REPO=david-acm/farkle TIMESTAMP=2026-05-29T10:00:00Z bash "$GEN" >/dev/null
[ -f "$PAGES/runs/1001/HappyPath.webm" ]; check "video copied" $?
[ -f "$PAGES/runs/1001/metadata.json" ]; check "metadata written" $?
[ -f "$PAGES/runs/1001/index.html" ]; check "per-run page written" $?
[ -f "$PAGES/index.html" ]; check "root index written" $?
[ -f "$PAGES/.nojekyll" ]; check ".nojekyll present" $?
grep -q '<video' "$PAGES/runs/1001/index.html"; check "per-run page embeds <video>" $?
grep -q 'HappyPath.webm' "$PAGES/runs/1001/metadata.json"; check "metadata lists videos" $?

echo "Test 2: a failing run with no videos still publishes"
mkdir -p "$WORK/v2"
PAGES_DIR="$PAGES" VIDEOS_DIR="$WORK/v2" RUN_ID=1002 PR_NUMBER=42 \
  STATUS=failure REPO=david-acm/farkle TIMESTAMP=2026-05-29T11:00:00Z bash "$GEN" >/dev/null
[ -f "$PAGES/runs/1002/index.html" ]; check "failed run page written" $?
grep -q '❌' "$PAGES/index.html"; check "root table shows failure badge" $?
# Newest first: 1002 must appear before 1001 in the root table.
[ "$(grep -n 'runs/1002/' "$PAGES/index.html" | head -1 | cut -d: -f1)" -lt \
  "$(grep -n 'runs/1001/' "$PAGES/index.html" | head -1 | cut -d: -f1)" ]
check "newest run listed first" $?

echo "Test 3: age-based pruning (>90 days)"
OLD="$(date -u -d '120 days ago' +%Y-%m-%dT%H:%M:%SZ)"
PAGES_DIR="$PAGES" VIDEOS_DIR="$WORK/v2" RUN_ID=900 STATUS=success TIMESTAMP="$OLD" bash "$GEN" >/dev/null
# Re-run a fresh build to trigger pruning of the just-added old run.
PAGES_DIR="$PAGES" VIDEOS_DIR="$WORK/v2" RUN_ID=1003 STATUS=success \
  TIMESTAMP=2026-05-29T12:00:00Z bash "$GEN" >/dev/null
[ ! -d "$PAGES/runs/900" ]; check "run older than 90 days pruned" $?

echo "Test 4: count-based pruning (MAX_RUNS)"
P2="$WORK/pages2"; mkdir -p "$P2"
for r in 10 11 12; do
  PAGES_DIR="$P2" VIDEOS_DIR="$WORK/v2" RUN_ID=$r STATUS=success MAX_RUNS=2 \
    TIMESTAMP=2026-05-29T12:00:00Z bash "$GEN" >/dev/null
done
[ ! -d "$P2/runs/10" ] && [ -d "$P2/runs/11" ] && [ -d "$P2/runs/12" ]
check "only newest MAX_RUNS kept" $?

if [ "$fail" -ne 0 ]; then
  echo "FAILED"; exit 1
fi
echo "All tests passed."
