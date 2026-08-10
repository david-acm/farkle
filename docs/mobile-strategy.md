# Mobile strategy: publishing HotDice to iOS + Android

**Decision (see [ADR 0008](decisions/0008-mobile-via-maui-blazor-hybrid.md)):** ship mobile as a
**.NET MAUI Blazor Hybrid** app that hosts our existing Razor UI in a `BlazorWebView`, with the UI
extracted into a **shared Razor Class Library (RCL)** used by both the Blazor WASM website and the
MAUI app. **Keep the web app.** The backend stays as the existing API + SignalR hub.

This doc records the research behind that decision, the concrete work-list for *this* codebase, and a
phased plan. It is a point-in-time analysis (late 2025 / early 2026, .NET 10 LTS).

## Why Hybrid, not native XAML

| | MAUI Blazor Hybrid | MAUI native (XAML) |
|---|---|---|
| **UI reuse** | **~90–100%** — Razor components, MudBlazor, `.razor.css`, CSS-3D dice, BlazorState, SignalR .NET client run essentially unchanged in a `BlazorWebView` | **~0%** — XAML renders native controls, so *no* Razor/MudBlazor/CSS reuse; the entire UI + state layer is rewritten. Only non-UI C# (domain, SignalR client, Kiota client) carries over |
| **Effort** | Medium — a shell project + plumbing + per-platform polish | Highest — full UI rebuild |
| **Native feel** | High (native shell + native APIs); UI is web-rendered | Highest (native controls) |
| **App Store 4.2 risk** | Low (local content, native shell) | Lowest |

For a two-player, turn-based dice game, native-control fidelity buys little, and Hybrid keeps the
UI investment. The dividing line is exact: **Hybrid keeps the web UI; native keeps only the C# below
the UI.**

## Why keep the web app (and why a shared RCL)

Microsoft's `maui-blazor-web` template exists specifically to share **one RCL of Razor components**
across a Blazor WebAssembly site *and* a MAUI Blazor Hybrid app. So keeping the browser app is nearly
free once components move into the RCL — it stays the zero-install channel and a fallback. The
backend is untouched.

## Why not the lighter PWA / wrapper routes (given we want iOS)

A Blazor WASM **PWA** is the cheapest, full-reuse option, but **Apple does not list PWAs in the App
Store** — you only get a Safari "Add to Home Screen" web-clip. A **TWA** lists cleanly on Google Play,
but the iOS wrapper route risks Apple **Guideline 4.2** rejection. Since iOS *store presence* is a
goal, Hybrid is the lowest-risk way to get there while reusing the UI. (A PWA is still worth doing as
an interim/companion — see Phase 0.)

## Work-list for this codebase

Drag-and-drop is **not** a factor — set-aside is already tap-to-select (`Blazor.Dice.DiceTray`, #196),
which is natively touch-friendly. The remaining items are standard standalone-client plumbing:

| # | Item | Why | Effort |
|---|---|---|---|
| 1 | **Absolute backend URL + server CORS + token auth** | Today the client inherits its origin from the WASM host (`HostEnvironment.BaseAddress`); a standalone app must target the Azure API and `/hubs/game` explicitly, with CORS opened for the app origin and a real token (no same-origin cookie) | Low–med |
| 2 | **SignalR mobile lifecycle** | OSes suspend sockets on background (iOS won't hold WebSockets backgrounded). Connect on foreground, disconnect on background, `WithAutomaticReconnect()`, re-fetch the game snapshot on resume — building on the existing `RemoteTurnChanged` + rehydrate | Low |
| 3 | **iOS static-asset packaging** | MudBlazor's `_content/…` CSS/JS can fail to load on WKWebView; .NET 10's `BlazorWebView` request interception (or copy-to-local-`wwwroot`) fixes it | Low |
| 4 | **Drop `@rendermode`; async-safe JS interop** | MAUI is always interactive and throws on explicit render modes; no synchronous JS interop in a WebView (our `window.*` helpers already use async `InvokeVoidAsync`) | Low |
| 5 | **Store setup** | iOS: Mac + Xcode + Apple Developer Program ($99/yr) + signing + **privacy manifest**; Android: Play Console ($25) + keystore; **on-device** testing before submit | One-time |

Note: WebView rendering is ~32–37% slower than WASM/Server due to .NET↔WebView marshalling — immaterial
for a turn-based game **provided animation stays in CSS** (it does) and interop isn't chatty per-frame.

## Phased plan

- **Phase 0 (optional, hours):** make the existing WASM app an installable **PWA** — instant Android
  (via TWA) + iOS home-screen presence while the real app is built. Interim only; no iOS *store* listing.
- **Phase 1 (done, #348):** the Razor UI lives in **`src/HotDice.Ui`**, consumed by both the WASM
  site (`WebApp.Client`, now just an entry point) and `HotDice.Shell`, which hosts the shared
  `Routes` component and registers the same services against an absolute backend URL
  (`HOTDICE_BACKEND_URL`). Namespaces stayed `WebApp.Client.*` — same trade as ADR 0006.
  **CORS and on-device verification are still open.**
- **Phase 2 (in progress, #339):** verify MudBlazor + CSS-3D dice + tap-to-select on **WKWebView** and
  Android WebView on real hardware; fix iOS static assets; per-platform CSS tweaks. The host page
  already references MudBlazor's `_content/` assets and the scoped-CSS bundle — that it *compiles* says
  nothing about whether WKWebView serves them, which is exactly the risk below. The **device UI tier**
  ([ADR 0011](decisions/0011-mobile-ui-test-driver.md), `tests/HotDice.DeviceTests`) now drives this on
  a real Android emulator per PR (`CI - Mobile Device UI`); the iOS gate is the follow-up that closes
  the WKWebView half. Runbook: [`docs/mobile-device-testing.md`](mobile-device-testing.md).
- **Phase 3:** SignalR mobile lifecycle + reconnect + resume re-sync; optionally add **push
  notifications** (APNs/FCM) for "your turn" alerts when backgrounded.
- **Phase 4:** privacy manifest, signing, **on-device** testing, submit to both stores.

## Key risks (ranked)

1. **Blank-screen/crash-on-device at launch → App Store rejection.** The dominant real-world Hybrid
   submission failure is a WebView startup crash that only appears on physical devices / new iOS
   versions. **Test on real hardware.**
2. **WebView render/interop overhead & cold start.** Mitigate with lazy loading, trim/AOT, batched
   interop, background WebView init. Fine for this app if animation stays in CSS.
3. **Android System WebView instability.** It's an updatable OS component; a Play update can regress the
   app in the field.
4. **iOS toolchain friction.** Mac/Xcode, signing, and a missing/incomplete `PrivacyInfo.xcprivacy` are
   common rejection causes.
5. **SignalR won't survive backgrounding.** Needs lifecycle-aware connect/disconnect + reconnect +
   resume re-sync (+ push fallback).
6. **Guideline 4.2 itself is low risk** — keep all Razor/HTML content bundled locally (don't load a
   remote URL into the WebView) to stay clear of 4.2 / 2.5.2.

## Sources

Reuse & Hybrid model:
- https://learn.microsoft.com/aspnet/core/blazor/hybrid/
- https://learn.microsoft.com/aspnet/core/blazor/hybrid/class-libraries/ (share via RCL)
- https://learn.microsoft.com/aspnet/core/blazor/hybrid/tutorials/maui-blazor-web-app
- https://learn.microsoft.com/aspnet/core/blazor/hosting-models/#blazor-hybrid
- https://learn.microsoft.com/dotnet/maui/user-interface/controls/blazorwebview

MudBlazor / WebView specifics:
- https://github.com/MudBlazor/MudBlazor/issues/6558 (iOS static assets / WKWebView)
- https://github.com/dotnet/maui/issues/33286 (static web assets on iOS)
- https://github.com/dotnet/maui/issues/28667 (Hybrid render ~32–37% slower — interop marshalling)
- https://github.com/dotnet/maui/issues/10002 (Android System WebView instability)

Store / publishing:
- https://developer.apple.com/app-store/review/guidelines/ (4.2 Minimum Functionality; 2.5.2)
- https://learn.microsoft.com/dotnet/maui/ios/deployment/publish-app-store
- https://learn.microsoft.com/dotnet/maui/android/deployment/publish-google-play
- https://learn.microsoft.com/dotnet/maui/ios/privacy-manifest
- https://learn.microsoft.com/en-us/answers/questions/972889/ (works in simulator, fails on device)

Real-time on mobile:
- https://montemagno.com/real-time-communication-for-mobile-with-signalr/
- https://developer.apple.com/forums/thread/716118 (no background WebSockets on iOS)
- https://learn.microsoft.com/aspnet/signalr/overview/guide-to-the-api/handling-connection-lifetime-events

Lighter alternatives:
- https://learn.microsoft.com/aspnet/core/blazor/progressive-web-app/ (Blazor PWA)
- https://www.mobiloud.com/blog/publishing-pwa-app-store (PWA store presence; iOS cannot list a bare PWA)
- https://developers.google.com/codelabs/pwa-in-play (TWA on Google Play)
