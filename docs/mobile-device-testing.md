# Mobile device UI testing — runbook

The device tier (#339, [ADR 0011](decisions/0011-mobile-ui-test-driver.md)) drives the **real
packaged app** through one happy path per platform with **Appium**, against the app's **WebView
context**, and attaches screenshots + video to the PR. It proves only what it uniquely can (ADR 0010):
the app launches, renders in the WebView, and talks to a real network. Everything deeper is covered
by the device-free tiers.

- **Test project:** `tests/HotDice.DeviceTests` (kept out of `HotDice.sln`, like the shell).
- **One-command local run:** `tests/scripts/device-happy-path.sh` (see below).
- **CI gate:** `.github/workflows/mobile-device-uitest.yml` (Android per-PR; iOS + nightly breadth are
  follow-ups on #339).

## Running it locally

Everything is configured through `UITEST_*` environment variables, so the local run and CI run the
same test binary. Boot a simulator/emulator first, then:

```bash
# Android (a booted AVD + `adb` on PATH):
UITEST_PLATFORM=android \
UITEST_BACKEND_URL=http://10.0.2.2:5000/ \
  bash tests/scripts/device-happy-path.sh

# iOS (a booted simulator + Xcode):
UITEST_PLATFORM=ios \
UITEST_BACKEND_URL=http://127.0.0.1:5000/ \
  bash tests/scripts/device-happy-path.sh
```

The wrapper builds the app (unless `UITEST_APP_PATH` is set), resolves and **pins** the device UDID,
starts an Appium server (unless `UITEST_APPIUM_URL` is set), records video, runs the one UI class in
isolation, and drops all artifacts in `test-results/device/<platform>/`.

### The `UITEST_*` variables

| Variable | Meaning | Default |
|---|---|---|
| `UITEST_PLATFORM` | `android` or `ios`. Unset ⇒ the test **skips**. | `android` (wrapper) |
| `UITEST_APP_PATH` | Absolute path to the built app (`.apk`/`.app`). | built by the wrapper |
| `UITEST_UDID` | Target device UDID/serial — **pin it** (see gotchas). | first booted device |
| `UITEST_APPIUM_URL` | Appium server URL. | `http://127.0.0.1:4723/` |
| `UITEST_BACKEND_URL` | Backend the app + the "second player" join hit. | `http://10.0.2.2:5000/` |
| `UITEST_DIAG_DIR` | Screenshots/video/logs output dir. | `test-results/device/<platform>` |
| `UITEST_BUNDLE_ID` | iOS bundle id for (re)launch. | `com.davidacm.hotdice` |

## Capture gotchas (symptom → cause → fix)

These are the reference app's hard-won lessons, ported because they each cost real debugging time.

### The video is truncated / won't play
- **Cause:** the recorder was ended with a plain `kill` (SIGTERM/SIGKILL). `simctl recordVideo` and
  `screenrecord` only flush and write the container's moov atom on **SIGINT**.
- **Fix:** finalize with `kill -INT "$REC_PID"` and **`wait`** for it before collecting the file. The
  wrapper's `finalize_recording` does exactly this; don't shortcut it.

### The wrong device is driven or recorded
- **Cause:** an unpinned run picks "the first booted device", and CI/local often has more than one
  (a leftover simulator, a second AVD). The driver attaches to one and the recorder to another.
- **Fix:** always resolve and **pin `UITEST_UDID`**, and pass that same UDID to `adb -s` /
  `simctl io <udid>`. The wrapper resolves once and exports it for both.

### `No WEBVIEW context appeared`
- **Cause:** the Hybrid app creates its WebView a beat after launch; querying contexts too early sees
  only the native context. On Android the WebView also needs to be debuggable and have a page.
- **Fix:** poll `driver.Contexts` until a `WEBVIEW_*` appears before switching (the harness does, up to
  the 120 s hydration timeout). On Android the driver sets `ensureWebviewsHavePages` +
  `nativeWebScreenshot`. A genuinely absent WebView after the timeout is the real bug the gate exists
  to catch — the #1 Hybrid store-rejection risk (`docs/mobile-strategy.md`).

### Flaky state between runs / a stale WebDriverAgent (iOS)
- **Cause:** reusing a process or an orphaned WDA session leaks driver/session state across tests.
- **Fix:** run the UI class **in isolation** (the wrapper filters to the single class; CI runs only
  this project). Kill stale WDA/simulators between runs if you see session-id errors.

### The emulator boot dominates the wall clock (Android CI)
- **Cause:** cold-booting an AVD every run is slow.
- **Fix:** CI uses `reactivecircus/android-emulator-runner` with **KVM** and an **AVD snapshot cache**
  keyed on the API level, so subsequent runs restore from snapshot. Budget is ≲15 min/platform
  including boot + app build; if it drifts over, cut scope rather than raise the timeout (#339).

### The app can't reach the backend from the emulator
- **Cause:** `localhost` inside the Android emulator is the emulator, not the host.
- **Fix:** use `http://10.0.2.2:<port>/` for the host loopback (the wrapper's default). iOS simulators
  share the host network, so `http://127.0.0.1:<port>/` works there.

### The app renders but nothing happens after "Start" (no lobby / stuck on the landing page)
- **Cause:** the backend round-trip (`POST /api/games`) never completed — either the backend is
  unreachable, or Android blocked the request as **cleartext** (http is disallowed by default).
- **Fix:** two parts. (1) Point the app at a reachable backend — the CI gate boots the real WebApp
  locally (`dotnet run` on plain http, Postgres service) and **bakes** its host-loopback address into
  the APK with `-p:HotDiceBackendUrl=http://10.0.2.2:5000` (a device can't read the host's
  `HOTDICE_BACKEND_URL` env var). (2) Allow cleartext to that host: `network_security_config.xml`
  permits http **only** to `10.0.2.2`/`localhost`, so a dev/CI build reaches a local backend while
  everything else stays HTTPS-only. Running against the *deployed* backend is #345's post-deploy job.
- **Aside:** in Blazor Hybrid, navigation doesn't change the WebView's document URL — detect a page by
  its DOM (`[data-testid='lobby']`), never by `driver.Url`.

## What the happy path proves

Launch (no blank WebView) → enter a name → start a game → a **SignalR-pushed** lobby update (a second
player joins from outside the device) → roll → set a die aside → **keep** (asserting the turn score
banks). Die faces aren't in the DOM (a 3-D CSS cube) and Appium can't intercept the `/rolls` response
the way Playwright does, so Keep needs a guaranteed scoring die: the gate boots the backend with
**`Dice:Scripted=true`** (a config-gated host seam — every roll is six 1s, so slot 0 always scores;
production never sets it). The same flag is what #345's post-deploy check reuses.
