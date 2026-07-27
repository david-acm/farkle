# 8. Ship mobile (iOS + Android) via .NET MAUI Blazor Hybrid over a shared Razor Class Library

Status: **Accepted** — direction chosen; implementation is phased and deferred (see
[`docs/mobile-strategy.md`](../mobile-strategy.md) for the plan, the work-list, and sources).

## Context

We want HotDice available as an installable app on the **Apple App Store and Google Play** (iOS +
Android; no desktop for now). The current client is a **Blazor WebAssembly** app (hosted by the Blazor
Server host) built on **MudBlazor**, **BlazorState**, a **Kiota**-generated API client, and the
**SignalR .NET client** for real-time two-player multiplayer, plus **CSS-3D dice**. The backend is the
existing ASP.NET Core (Wolverine.HTTP + Marten) API with a SignalR hub at `/hubs/game`, deployed to
Azure Container Apps. Real-time multiplayer is core to the game.

Two primary paths were weighed — **.NET MAUI native (XAML)** vs **.NET MAUI Blazor Hybrid**
(`BlazorWebView`) — plus lighter alternatives (installable **PWA**, a **TWA**/Capacitor wrapper).

Two facts about *this* codebase shaped the decision:

- The set-aside interaction is already **tap-to-select** (a reusable `Blazor.Dice.DiceTray`, #196), not
  HTML5 drag-and-drop. Touch DnD-in-a-WebView — the usual reuse blocker — **does not apply here**.
- The client today derives its origin from the host that serves the WASM
  (`HostEnvironment.BaseAddress`); a standalone app must target an **absolute** backend URL.

## Decision

Ship the mobile app as a **.NET MAUI Blazor Hybrid** app that hosts our existing Razor UI in a
`BlazorWebView`, with the UI **extracted into a shared Razor Class Library (RCL)** consumed by *both*
the Blazor WASM website and the MAUI app (the `maui-blazor-web` pattern). **Keep the web app.** The
backend stays exactly as it is — an HTTP API + SignalR hub the app talks to over the network.

## Rationale

- **Reuse.** Blazor Hybrid reuses ~**90–100%** of the existing UI (Razor, MudBlazor, `.razor.css`,
  BlazorState, CSS-3D dice, SignalR .NET client) essentially unchanged; MAUI **native (XAML) reuses
  ~0%** of the UI (only the non-UI C# carries over) — it is a full UI + state rewrite.
- **Store risk.** A Hybrid app ships its UI **locally** in a native shell and can call native APIs, so
  it reads as "app-like" and clears Apple **Guideline 4.2** the same way Capacitor/React-Native apps
  do. A raw **PWA cannot be listed on the iOS App Store** at all, so it can't be the sole path.
- **No UI rework.** Because set-aside is already tap-based and the dice animation is CSS-driven, there
  is no touch-DnD problem and the ~32–37% WebView render overhead is immaterial for a turn-based game.
- **Keep-the-web is nearly free** once the UI is an RCL shared by both clients.

## Consequences

- New standalone-client plumbing: **absolute backend URL + server CORS + token auth** (no same-origin
  cookie), **SignalR mobile lifecycle** (foreground connect / background disconnect,
  `WithAutomaticReconnect`, resume-time snapshot re-sync — building on the existing `RemoteTurnChanged`
  + rehydrate), **iOS static-asset packaging** for MudBlazor `_content` on WKWebView, dropping
  `@rendermode` and using **async-safe JS interop** in the shared RCL.
- Toolchain/one-time: a **Mac + Xcode + Apple Developer Program ($99/yr)** for iOS, **Google Play
  ($25)**, an Apple **privacy manifest**, code signing, and **on-device** testing before submit (the
  dominant real-world Hybrid rejection is a blank-screen crash that only shows on hardware).
- The web app remains a first-class client sharing one RCL of components — a maintenance win, not a
  cost.

## Alternatives considered

- **MAUI native (XAML)** — rejected: ~0% UI reuse, full rewrite of UI + state for little benefit on a
  two-player dice game.
- **Installable PWA (only)** — rejected as the sole path: cheapest and full reuse, but **no iOS App
  Store** listing. Still worth doing as an interim/companion channel (see the strategy doc's Phase 0).
- **TWA / Capacitor wrapper** — Android is fine; the iOS wrapper carries real Guideline 4.2 rejection
  risk without added native functionality.
