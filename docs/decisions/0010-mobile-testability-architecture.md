# 10. Mobile testability: a shared client core, a thin shell, and seams that earn their keep

Status: **Accepted** (#336, epic #334). Supplements [ADR 0008](0008-mobile-via-maui-blazor-hybrid.md)
(MAUI Blazor Hybrid over a shared RCL) and applies the translation decisions in
[ADR 0009](0009-mobile-practice-port-for-hotdice.md).

## Context

HotDice ships as a MAUI Blazor Hybrid app (ADR 0008). Before any feature work, the question is where
the testable boundary sits — because the answer decides whether a mobile change gets feedback in
seconds on a laptop or in tens of minutes on an emulator.

The reference app answered this by keeping ViewModels, services and handlers in a MAUI-free `Core`
library, reaching platform capabilities through interfaces (`INavigationService`,
`ISecureStorageService`, `IConnectivityService`, `IDialogService`, `IMainThreadDispatcher`) that the
MAUI head implemented. That is a seam-heavy design, and this repo has spent three ADRs
([0004](0004-marten-native-domain.md), [0005](0005-embrace-signalr-in-the-core.md),
[0006](0006-merge-contracts-and-sharedkernel.md)) deleting exactly that kind of interface from the
server — *embrace, don't abstract*. Importing the pattern wholesale would contradict the repo;
rejecting it wholesale would give up off-device testing. Neither is right.

A concrete constraint settles most of it: **the MAUI shell cannot reference `WebApp.Client`**, which
is a Blazor WebAssembly *application* project. Any client logic both clients need has to live
somewhere else regardless of philosophy.

## Decision

### 1. A shared `Farkle.Client` library is the testable core

`src/Farkle.Client` (net10.0) holds the client logic both the website and the app need — the
realtime session, the app-lifecycle rules, and the API connection. It references no UI framework
and no MAUI, so **all of it runs under plain desktop unit tests** (`tests/Farkle.Client.Tests`) on
the same Linux runner as the rest of CI.

This is the same move `Farkle.Shared` made for the WASM client (ADR 0006): a framework-free leaf
exists when a second consumer genuinely cannot take the dependency. It is a mechanical necessity,
not a purity boundary.

### 2. The UI's testable core is the RCL, not a ViewModel layer

In a Hybrid app the UI *is* Razor, so the reference app's MVVM/`Core`-ViewModel row does not port —
components are the unit, and bUnit is the test. `Blazor.Dice` already demonstrates the pattern (an
RCL consumed by the WASM client and covered by `Blazor.Dice.Tests` + `Farkle.SpaTests`), and the
shell renders one of its components today to prove Razor from an RCL runs unchanged in the WebView.

Migrating the remaining game UI out of `WebApp.Client` into a shared RCL is `docs/mobile-strategy.md`
**Phase 1** and lands as its own change — it touches every page and the Playwright/storyboard
coverage, and mixing it into this decision would obscure both.

### 3. Seams are admitted one at a time, on evidence

A seam earns its place **only** when it makes something testable off-device that otherwise would not
be. Two qualify today:

| Seam | Why it exists |
|---|---|
| `IGameHubSession` | Lets the lifecycle rules be driven by a fake with no SignalR server. `GameHubService` still uses `HubConnection` directly behind it — the seam is narrower than the service. |
| `IGameSnapshotRefresher` | The client owns *where* state lives (BlazorState on the web); the core only decides *when* a re-sync is needed. |

Deferred until something concrete needs them — not adopted by analogy: secure token storage,
connectivity, dialogs, navigation, main-thread dispatch. When one is needed, it gets a row here and a
test that would fail without it. Blanket-porting the reference app's five interfaces is explicitly
rejected.

### 4. The SignalR mobile lifecycle is the core's job, the shell only forwards events

A phone suspends sockets on background — iOS will not hold a WebSocket backgrounded — and SignalR
replays nothing missed while away. So `GameSessionLifecycle` (in the core) owns the rules:

- **background** → disconnect deliberately;
- **foreground** → reconnect, **then** re-fetch the snapshot (that order is load-bearing: refreshing
  before the socket is live reopens the very gap the refresh exists to close);
- resume is **idempotent** (both platforms can raise it more than once per return);
- a **failed reconnect leaves the session retryable** rather than throwing — a resume routinely beats
  the network back, and throwing would wedge the session with no path to recover.

`MauiAppLifecycleBridge` in the shell maps `Window.Activated/Deactivated/Resumed/Stopped` onto those
calls and contains no logic. If it ever grows a branch, that branch belongs on the core side.

### 5. The mobile client consumes the generated API client

`FarkleApiConnection` builds the Kiota `FarkleApiClient` over an **injected `HttpMessageHandler`**
(so tests drive real requests with no server) against an **absolute** backend URL — the web client
inherits its origin from the host that served the WASM; a standalone app has none. It rejects a
non-http(s) URL: on Unix `Uri.TryCreate("/api", Absolute, …)` succeeds as a `file:` URI, so
absoluteness alone would let a relative path through and fail on a device as an opaque connection
error. `verify-generated` therefore covers the mobile client too, for free.

It is a *connection* rather than a static factory because something must own the `HttpClient` and its
handler chain: they live for the app's lifetime (registered as a singleton, disposed by the
container), and building one per request would leak sockets. It disposes only the transport it
creates — a caller-supplied handler stays the caller's to dispose, which is what keeps test doubles
and shared handlers safe.

### 6. The shell stays out of `Farkle.sln`

A solution-wide `dotnet build` on a runner without the MAUI workloads would fail, so
`HotDice.Shell` is built by its own workflow (`CI - Mobile Shell`) after installing
`maui-android`. The shell multi-targets iOS **only on macOS**, which is what lets a Linux runner
build the Android head without the Apple toolchain.

## Consequences

- Mobile logic is testable in **milliseconds on a laptop**; the device tier (#339) is left to prove
  only what it uniquely can — that the app launches, renders in the WebView, and talks to a real
  network.
- One more project in the graph, and `IGameHubService` now extends `IGameHubSession` (its
  `ConnectAsync`/`DisconnectAsync` gained a `CancellationToken`). The web app is otherwise untouched
  and its 120 bUnit tests pass unchanged.
- A trap is documented in the shell's csproj: the root `Directory.Build.props` sets a **singular**
  `<TargetFramework>net8.0</TargetFramework>`, which silently wins over `TargetFrameworks` in a
  multi-targeting project and surfaces as a confusing RID-mismatch error. The shell clears it.
- The deferred seams are a standing invitation to re-open this ADR with evidence, which is the
  intended way to grow the list.
