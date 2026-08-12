# 11. The device UI-test driver: Appium over the WebView, not Playwright

Status: **Accepted** (#339, epic #334). Implements the "Device/UI testing & evidence" row of
[`docs/mobile-practices-inventory.md`](../mobile-practices-inventory.md) and builds on
[ADR 0010](0010-mobile-testability-architecture.md) (the device tier proves only what it uniquely can).

## Context

The device tier (#339) drives the **real packaged app** through one happy path per platform — cold
launch with no blank WebView, enter a name, start/join a game, roll, set a die aside, keep, and observe
one SignalR-pushed update — and attaches screenshots + video to the PR. Everything deeper stays in the
device-free tiers (domain/decider, `HotDice.Client.Tests`, bUnit, Playwright, storyboard).

HotDice is a **MAUI Blazor Hybrid** app (ADR 0008): the UI is Razor rendered inside a platform WebView
(`WKWebView` on iOS, Android System WebView). So the automation has to reach **into the WebView's DOM**
and drive the same elements the Playwright suite already drives — the `data-testid` map is shared with
`GameHappyPathShould`. Two candidate drivers were weighed, as the issue asked (spike first, record here).

## Decision

**Use Appium** (UiAutomator2 on Android, XCUITest on iOS) driving the app's **WebView context**, with a
.NET client (`Appium.WebDriver`) so the tests live beside the rest of the xUnit suites and reuse the
existing selector map. Configuration is entirely through `UITEST_*` environment variables so the same
test binary runs locally against a booted simulator/emulator and in CI.

### Why not Playwright against the WebView

Reusing the existing `GameFlow.cs` Playwright selectors verbatim was tempting, and Playwright *can*
attach to an Android System WebView over CDP. But **Playwright cannot drive the iOS `WKWebView`** — there
is no Chrome DevTools Protocol endpoint on WebKit, and Playwright's WebKit support is its own bundled
browser, not the app's embedded web view. That structurally fails the "**both platforms**" acceptance
criterion: it would give an Android-only gate and leave iOS with no driver at all. A gate that covers one
of the two shipping platforms is not the gate #339 asks for.

Appium, by contrast, drives **both** platform WebViews through one API by switching into the `WEBVIEW_*`
context (Android) / the WebKit web context (iOS), where element location is ordinary CSS/XPath against the
live DOM — the same elements Playwright targets. One driver, one test, both platforms.

### Why Appium over lighter alternatives (Maestro)

Maestro is simpler and less flaky, but its WebView-context assertions are coarser and it is a new tool
outside the reference app's proven toolchain. The reference app already paid down the Appium learning
cost (UiAutomator2/XCUITest, WebView context switching, the capture gotchas), and that lore ports
directly (see [`docs/mobile-device-testing.md`](../mobile-device-testing.md)). We keep the door open to
revisit Maestro if Appium's flakiness outweighs its coverage — this ADR is re-openable on evidence, like
the seams in ADR 0010.

### Selectors and config

- **Selectors** are the shared `data-testid` map (`start-new-game`, `lobby`, `roster-player`,
  `start-game-button`, `my-turn-indicator`, `tray-die`, …) plus button text (`Roll`/`Keep`/`Pass Turn`),
  located by CSS/XPath inside the WebView context. No device-only selector scheme is introduced.
- **`UITEST_*` env vars** carry every runner-specific value (`UITEST_PLATFORM`, `UITEST_UDID`,
  `UITEST_APP_PATH`, `UITEST_APPIUM_URL`, `UITEST_BACKEND_URL`), mirroring the existing
  `HOTDICE_BACKEND_URL` / `E2E_STEP_DELAY_MS` / `PLAYWRIGHT_CHROMIUM_EXECUTABLE_PATH` precedents. A run
  with no config **skips** (an xUnit skip, not a failure), so the suite is inert wherever no device is
  attached — including the normal `dotnet test HotDice.sln` job, which never sees this project because,
  like the shell (ADR 0010 §7), it is kept **out of the solution**.

## Consequences

- The device tests are net-new (`tests/HotDice.DeviceTests`), built and run only by the device workflow;
  they add an Appium/Selenium dependency to that project alone and never enter the coverage gate (a device
  test exercises the shell, which ADR 0010 §6 deliberately leaves ungated).
- **First increment (this PR):** the harness, the shared selectors, the evidence loop, the local wrapper,
  the runbook, and the **Android** per-PR gate as `continue-on-error`. The **iOS** gate, the **nightly
  breadth matrix**, and the flip from `continue-on-error` to **required** are follow-ups, tracked on #339.
- **Deterministic dice for Keep:** the "keep a *scoring* die" step needs a guaranteed scoring die,
  because die faces are not in the DOM (a 3-D CSS cube — the Playwright suite intercepts the `/rolls`
  response instead, which Appium cannot do as cleanly). The gate boots the backend with
  `Dice:Scripted=true`, a config-gated seam in the host that swaps in a deterministic provider (every
  roll is six 1s, so slot 0 always scores); production leaves the flag unset and uses the real RNG.
  The happy path therefore proves launch → name → start → SignalR-pushed join → roll → **set-aside** →
  **keep** (asserting the turn score banks), and the same flag is what #345's post-deploy check reuses.
</content>
</invoke>
