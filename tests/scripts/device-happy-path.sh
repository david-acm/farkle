#!/usr/bin/env bash
# Reproduce the on-device happy path (#339, ADR 0011) against a booted simulator/emulator, with
# the SAME artifacts CI produces: per-state screenshots + a video, in a diagnostics dir. One
# command, driven entirely by UITEST_* env vars so local and CI runs are identical.
#
#   Android:  UITEST_PLATFORM=android  bash tests/scripts/device-happy-path.sh
#   iOS:      UITEST_PLATFORM=ios      bash tests/scripts/device-happy-path.sh
#
# Prereqs (see docs/mobile-device-testing.md): a booted emulator/simulator, an Appium server
# (auto-started here if UITEST_APPIUM_URL is unset and `appium` is on PATH), the maui workload,
# and a reachable backend (UITEST_BACKEND_URL). The capture gotchas this script encodes — UDID
# pinning, SIGINT (not kill) to finalize the recording, class isolation — are the reference app's
# hard-won lessons; the runbook explains each.
set -euo pipefail

PLATFORM="${UITEST_PLATFORM:-android}"
REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
DIAG_DIR="${UITEST_DIAG_DIR:-$REPO_ROOT/test-results/device/$PLATFORM}"
SHELL_CSPROJ="$REPO_ROOT/src/HotDice.Shell/HotDice.Shell.csproj"
TEST_CSPROJ="$REPO_ROOT/tests/HotDice.DeviceTests/HotDice.DeviceTests.csproj"
CONFIGURATION="${UITEST_CONFIGURATION:-Release}"

rm -rf "$DIAG_DIR"; mkdir -p "$DIAG_DIR"
export UITEST_PLATFORM UITEST_DIAG_DIR="$DIAG_DIR"

log() { printf '\n\033[1;36m[device-happy-path]\033[0m %s\n' "$*"; }

# ── Resolve the target device UDID (pinning is load-bearing — an unpinned run can attach to or
#    record the WRONG device when several are booted). ─────────────────────────────────────────
resolve_udid() {
  if [ -n "${UITEST_UDID:-}" ]; then echo "$UITEST_UDID"; return; fi
  if [ "$PLATFORM" = "android" ]; then
    adb devices | awk 'NR>1 && $2=="device" {print $1; exit}'
  else
    xcrun simctl list devices booted -j | python3 -c \
      'import json,sys; d=json.load(sys.stdin)["devices"]; print(next((x["udid"] for v in d.values() for x in v if x.get("state")=="Booted"), ""))'
  fi
}

UDID="$(resolve_udid)"
[ -n "$UDID" ] || { echo "No booted $PLATFORM device found. Boot one first (see the runbook)." >&2; exit 1; }
export UITEST_UDID="$UDID"
# Hand the test the resolved absolute adb path — a bare "adb" isn't reliably on the .NET test
# process's PATH, so its device-level screenshots need the explicit binary.
export UITEST_ADB="$(command -v adb || echo adb)"
log "Target device: $UDID"

# ── Build the app if a prebuilt artifact wasn't supplied. ──────────────────────────────────────
if [ -z "${UITEST_APP_PATH:-}" ]; then
  if [ "$PLATFORM" = "android" ]; then
    log "Publishing the Android app…"
    dotnet publish "$SHELL_CSPROJ" -f net10.0-android -c "$CONFIGURATION" -p:AndroidPackageFormat=apk
    UITEST_APP_PATH="$(find "$REPO_ROOT/src/HotDice.Shell/bin" -name '*-Signed.apk' -print -quit)"
    [ -n "$UITEST_APP_PATH" ] || UITEST_APP_PATH="$(find "$REPO_ROOT/src/HotDice.Shell/bin" -name '*.apk' -print -quit)"
  else
    log "Building the iOS app for the simulator…"
    dotnet build "$SHELL_CSPROJ" -f net10.0-ios -c "$CONFIGURATION" -p:RuntimeIdentifier=iossimulator-arm64
    UITEST_APP_PATH="$(find "$REPO_ROOT/src/HotDice.Shell/bin" -name '*.app' -type d -print -quit)"
  fi
fi
export UITEST_APP_PATH
[ -n "$UITEST_APP_PATH" ] || { echo "Could not locate a built app artifact." >&2; exit 1; }
log "App artifact: $UITEST_APP_PATH"

# ── Start an Appium server if none was provided. ───────────────────────────────────────────────
APPIUM_PID=""
if [ -z "${UITEST_APPIUM_URL:-}" ]; then
  if command -v appium >/dev/null 2>&1; then
    log "Starting a local Appium server…"
    # Allow on-demand Chromedriver download so the driver can automate the WebView whatever the
    # System WebView's Chromium version is (paired with the chromedriverAutodownload capability).
    appium --log "$DIAG_DIR/appium.log" \
      --allow-insecure=uiautomator2:chromedriver_autodownload >/dev/null 2>&1 &
    APPIUM_PID=$!
    export UITEST_APPIUM_URL="http://127.0.0.1:4723/"
    sleep 5
  else
    echo "Appium is not on PATH and UITEST_APPIUM_URL is unset. Install appium or set the URL." >&2
    exit 1
  fi
fi

# ── Start screen recording, PINNED to the resolved UDID. Finalize with SIGINT (NOT kill) or the
#    container writes a truncated, unplayable file. ─────────────────────────────────────────────
VIDEO="$DIAG_DIR/happy-path.mp4"
REC_PID=""
start_recording() {
  if [ "$PLATFORM" = "android" ]; then
    # screenrecord caps at 180s; the opening flow is well under that.
    adb -s "$UDID" shell screenrecord --time-limit 180 /sdcard/happy-path.mp4 &
    REC_PID=$!
  else
    xcrun simctl io "$UDID" recordVideo --codec=h264 --force "$VIDEO" &
    REC_PID=$!
  fi
}
finalize_recording() {
  [ -n "$REC_PID" ] || return 0
  # SIGINT (the gotcha) so the recorder flushes a valid file instead of truncating. On Android the
  # local `adb shell screenrecord` client won't relay the signal to the device-side recorder, so
  # signal it there too — otherwise the wait blocks until screenrecord's own 180s limit.
  if [ "$PLATFORM" = "android" ]; then
    adb -s "$UDID" shell pkill -INT screenrecord >/dev/null 2>&1 || true
  fi
  kill -INT "$REC_PID" 2>/dev/null || true
  wait "$REC_PID" 2>/dev/null || true
  if [ "$PLATFORM" = "android" ]; then
    sleep 2
    adb -s "$UDID" pull /sdcard/happy-path.mp4 "$VIDEO" >/dev/null 2>&1 || true
    adb -s "$UDID" shell rm -f /sdcard/happy-path.mp4 >/dev/null 2>&1 || true
  fi
}
cleanup() {
  finalize_recording
  [ -n "$APPIUM_PID" ] && kill "$APPIUM_PID" 2>/dev/null || true
}
trap cleanup EXIT

log "Recording to $VIDEO"
start_recording

# ── Run the one UI class in isolation (a shared process can leak driver/session state). ─────────
set +e
dotnet test "$TEST_CSPROJ" -c "$CONFIGURATION" \
  --filter "FullyQualifiedName~DeviceHappyPathShould" \
  --logger "trx;LogFileName=device-happy-path.trx" \
  --results-directory "$DIAG_DIR" 2>&1 | tee "$DIAG_DIR/dotnet-test.log"
STATUS=${PIPESTATUS[0]}
set -e

# A SKIPPED happy path must not read as success — a skipped xUnit test exits 0, which would let an
# unconfigured/misdriven run report a false green. If the test skipped, fail loudly.
if grep -q "DriveTheOpeningFlowOnDevice \[SKIP\]" "$DIAG_DIR/dotnet-test.log" 2>/dev/null; then
  echo "::error::The device happy path SKIPPED — UITEST_* was not visible to the test process. Treating as failure."
  STATUS=1
fi

finalize_recording; trap - EXIT; [ -n "$APPIUM_PID" ] && kill "$APPIUM_PID" 2>/dev/null || true

# Evidence guard: a green run with no real evidence proves nothing (CLAUDE.md "evidence the change").
# The VIDEO is the primary visual proof, so a missing/blank one is FATAL — a real ~30s screen recording
# is comfortably over 50 KB, a truncated/blank one is a few KB. Per-state screenshots are supplementary
# (the video already shows the flow), so their absence is only a WARNING — with a breadcrumb left by the
# test explaining why any given frame was lost.
video_bytes="$([ -f "$VIDEO" ] && wc -c < "$VIDEO" 2>/dev/null || echo 0)"
if [ "${video_bytes:-0}" -lt 51200 ]; then
  echo "::error::Evidence check — video missing or under 50 KB (${video_bytes} bytes at $VIDEO); recording is blank or truncated."
  STATUS=1
fi
if [ -z "$(find "$DIAG_DIR" -maxdepth 1 -name '*.png' -print -quit 2>/dev/null)" ]; then
  echo "::warning::Evidence check — no per-state screenshots (*.png) captured in $DIAG_DIR; the video is the evidence for this run (see any *.capture-error.txt breadcrumb)."
fi

log "Artifacts in $DIAG_DIR:"; ls -1 "$DIAG_DIR" || true
exit $STATUS
