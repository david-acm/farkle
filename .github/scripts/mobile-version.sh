#!/usr/bin/env bash
#
# Play-safe monotonic version computation for the HotDice device builds (#338), shared by the
# Android `ApplicationVersion` (Play versionCode) and the iOS `CFBundleVersion`.
#
# The reference app derived `<yyyyMMdd><dailyRev>` from an Azure DevOps *daily* counter
# (`counter(DateStamp, 1)`). GitHub Actions has no per-day counter, but it has `github.run_number`
# — a repo-wide, strictly-increasing build counter. That is the natural, robust translation:
#   * version_code = run_number  → guaranteed strictly monotonic (Play's hard requirement), and
#     tiny relative to the ceiling (you'd need 2.1 billion runs to approach it).
#   * the date is preserved for humans in build_version (marketing.yyyyMMdd.run), which feeds the
#     informative CFBundleVersion / display string — you can still read *when* a build was cut.
# Mixing date*100+rev on GitHub is fragile (run_number is not day-scoped, so a day-boundary or a
# >99-builds/day day can make date+rev regress and break Play's monotonicity rule); run_number
# avoids that whole class of bug. See docs/mobile-signing-secrets.md.
#
# Inputs (env):
#   RUN_NUMBER        required — the monotonic build counter (github.run_number)
#   BUILD_DATE        optional — yyyyMMdd; defaults to today (UTC)
#   MARKETING_VERSION optional — the human version (CFShortVersionString / ApplicationDisplayVersion);
#                                defaults to 1.0
#
# Outputs: `key=value` lines on stdout, and appended to $GITHUB_OUTPUT when set:
#   version_code, display_version, build_version
#
set -euo pipefail

# Google Play caps versionCode at 2,100,000,000 (a signed-32-bit-ish limit). Overflow guard.
readonly PLAY_VERSION_CODE_MAX=2100000000

RUN_NUMBER="${RUN_NUMBER:-}"
BUILD_DATE="${BUILD_DATE:-$(date -u +%Y%m%d)}"
MARKETING_VERSION="${MARKETING_VERSION:-1.0}"

die() { echo "mobile-version: $*" >&2; exit 1; }

# RUN_NUMBER must be a positive integer (no leading +, no sign, no letters).
[[ "$RUN_NUMBER" =~ ^[0-9]+$ ]] || die "RUN_NUMBER must be a positive integer, got '${RUN_NUMBER}'"
# Strip leading zeros for the numeric comparison, then guard the range.
version_code=$((10#$RUN_NUMBER))
(( version_code >= 1 )) || die "version_code must be >= 1, got ${version_code}"
(( version_code <= PLAY_VERSION_CODE_MAX )) \
  || die "version_code ${version_code} exceeds the Google Play ceiling ${PLAY_VERSION_CODE_MAX}"

[[ "$BUILD_DATE" =~ ^[0-9]{8}$ ]] || die "BUILD_DATE must be yyyyMMdd, got '${BUILD_DATE}'"

display_version="$MARKETING_VERSION"
build_version="${MARKETING_VERSION}.${BUILD_DATE}.${version_code}"

emit() { # emit key value  → stdout, and $GITHUB_OUTPUT when present
  echo "$1=$2"
  [ -n "${GITHUB_OUTPUT:-}" ] && echo "$1=$2" >> "$GITHUB_OUTPUT"
  return 0
}

emit version_code    "$version_code"
emit display_version "$display_version"
emit build_version   "$build_version"
