# HotDice.DeviceTests

The device UI tier (#339, [ADR 0011](../../docs/decisions/0011-mobile-ui-test-driver.md)). Drives the
**real packaged MAUI Blazor Hybrid app** through one happy path per platform with **Appium**, against
the app's **WebView context**, reusing the same `data-testid` selectors as the Playwright suite.

- **Not in `HotDice.sln`** (like `HotDice.Shell`): the normal `dotnet test HotDice.sln` job has no
  device, so this project is built and run only by `mobile-device-uitest.yml`. With no `UITEST_*`
  config the single test **skips**, so a stray run anywhere is inert.
- **Run it:** `tests/scripts/device-happy-path.sh` (one command; boots/pins a device, records video,
  runs the class in isolation, collects artifacts).
- **Everything else** — the `UITEST_*` variables, the capture gotchas (UDID pinning, SIGINT video
  finalize, class isolation, WebView context switching, emulator boot/caching), and the known
  deterministic-Keep gap — is in [`docs/mobile-device-testing.md`](../../docs/mobile-device-testing.md).
