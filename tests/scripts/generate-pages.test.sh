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

echo "Test 5: a storyboard (screenshots) run writes a separate page"
P3="$WORK/pages3"; mkdir -p "$P3"
shots="$WORK/s1"; mkdir -p "$shots"
for f in 01-landing 02-lobby 03-roll 04-drag 05-keep 06-pass; do
  for vp in mobile medium large; do printf 'x' > "$shots/$f-$vp.png"; done
done
PAGES_DIR="$P3" MODE=screenshots SCREENSHOTS_DIR="$shots" RUN_ID=2001 PR_NUMBER=7 \
  BRANCH=feature/s COMMIT_SHA=abcdef1234567 STATUS=success \
  REPO=david-acm/farkle TIMESTAMP=2026-05-29T10:00:00Z bash "$GEN" >/dev/null
[ -f "$P3/runs/2001/storyboard.html" ]; check "storyboard page written" $?
[ ! -f "$P3/runs/2001/index.html" ]; check "screenshots mode does not write the videos page" $?
[ -f "$P3/runs/2001/screenshots/01-landing-mobile.png" ]; check "screenshot copied" $?
grep -q '<img' "$P3/runs/2001/storyboard.html"; check "storyboard page embeds <img>" $?
grep -q '>mobile<' "$P3/runs/2001/storyboard.html"; check "storyboard groups by viewport" $?
grep -q '01-landing-mobile.png' "$P3/runs/2001/metadata.json"; check "metadata lists screenshots" $?
grep -q 'storyboard.html' "$P3/index.html"; check "root table links to storyboard page" $?
grep -q '📸' "$P3/index.html"; check "root table shows a Storyboard column entry" $?

echo "Test 6: the two modes merge into one run without clobbering each other"
P4="$WORK/pages4"; mkdir -p "$P4"
vids2="$WORK/v6"; mkdir -p "$vids2"; printf 'x' > "$vids2/HappyPath.webm"
# Storyboard publishes first, then videos publishes into the same run.
PAGES_DIR="$P4" MODE=screenshots SCREENSHOTS_DIR="$shots" RUN_ID=3001 STATUS=success \
  TIMESTAMP=2026-05-29T10:00:00Z bash "$GEN" >/dev/null
PAGES_DIR="$P4" MODE=videos VIDEOS_DIR="$vids2" RUN_ID=3001 STATUS=success \
  TIMESTAMP=2026-05-29T10:05:00Z bash "$GEN" >/dev/null
grep -q 'HappyPath.webm' "$P4/runs/3001/metadata.json"; check "merge keeps videos" $?
grep -q '01-landing-mobile.png' "$P4/runs/3001/metadata.json"; check "merge preserves screenshots" $?
[ -f "$P4/runs/3001/index.html" ] && [ -f "$P4/runs/3001/storyboard.html" ]
check "both per-run pages exist after merge" $?
grep -q 'View storyboard screenshots' "$P4/runs/3001/index.html"; check "videos page cross-links to storyboard" $?
# Root table for the merged run links to both artifacts.
grep -q '🎬' "$P4/index.html" && grep -q '📸' "$P4/index.html"
check "root table shows both Videos and Storyboard entries" $?

if [ "$fail" -ne 0 ]; then
  echo "FAILED"; exit 1
fi
echo "All tests passed."
