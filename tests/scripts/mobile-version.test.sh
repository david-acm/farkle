#!/usr/bin/env bash
#
# Tests for .github/scripts/mobile-version.sh — the Play-safe monotonic version computation
# shared by the Android versionCode and the iOS CFBundleVersion (#338).
# Run locally with:  bash tests/scripts/mobile-version.test.sh
#
set -uo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
VER="$SCRIPT_DIR/.github/scripts/mobile-version.sh"

fail=0
check() { # check <description> <condition-result(0=pass)>
  if [ "$2" = "0" ]; then echo "  ✓ $1"; else echo "  ✗ $1"; fail=1; fi
}

# run <VAR=val ...> — invoke the script with the given env, capture stdout in $OUT and rc in $RC.
run() {
  OUT="$(env "$@" bash "$VER" 2>/dev/null)"; RC=$?
}

echo "Test 1: a normal build stamps a monotonic, Play-safe version"
run RUN_NUMBER=42 BUILD_DATE=20260726 MARKETING_VERSION=1.2
check "exits 0" "$RC"
echo "$OUT" | grep -qx 'version_code=42'; check "version_code = run number (42)" "$?"
echo "$OUT" | grep -qx 'display_version=1.2'; check "display_version = marketing version" "$?"
echo "$OUT" | grep -qx 'build_version=1.2.20260726.42'; check "build_version embeds marketing.date.run" "$?"

echo "Test 2: version_code is strictly monotonic in the run number"
run RUN_NUMBER=43 BUILD_DATE=20260726 MARKETING_VERSION=1.2
code43="$(echo "$OUT" | sed -n 's/^version_code=//p')"
[ "$code43" = "43" ] && [ "$code43" -gt 42 ]; check "run 43 > run 42" "$?"

echo "Test 3: over the Google Play versionCode ceiling (2,100,000,000) fails loudly"
run RUN_NUMBER=2100000001 BUILD_DATE=20260726
[ "$RC" -ne 0 ]; check "exits non-zero past the Play cap" "$?"

echo "Test 4: a non-integer run number is rejected"
run RUN_NUMBER=abc BUILD_DATE=20260726
[ "$RC" -ne 0 ]; check "exits non-zero on a non-integer run number" "$?"

echo "Test 5: BUILD_DATE defaults to today (UTC) when omitted"
run RUN_NUMBER=7 MARKETING_VERSION=1.0
today="$(date -u +%Y%m%d)"
echo "$OUT" | grep -qx "build_version=1.0.${today}.7"; check "defaults the date to today" "$?"

echo "Test 6: writes the same key=value pairs to \$GITHUB_OUTPUT"
tmp_out="$(mktemp)"
run RUN_NUMBER=9 BUILD_DATE=20260726 MARKETING_VERSION=1.0 GITHUB_OUTPUT="$tmp_out"
grep -qx 'version_code=9' "$tmp_out"; check "GITHUB_OUTPUT gets version_code" "$?"
rm -f "$tmp_out"

echo ""
if [ "$fail" = "0" ]; then echo "All mobile-version tests passed."; else echo "FAILURES above."; fi
exit "$fail"
