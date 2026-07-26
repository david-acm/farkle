# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Repository Overview

**Farkle** is a sample .NET application demonstrating Event Sourcing, CQRS, and **vertical-slice
architecture** on the **Critter Stack** (Marten + Wolverine). It implements the Greedy/Farkle dice
game as a backend API + a frontend (Blazor Server host + WASM client) with **real-time multiplayer**
over SignalR.

The codebase prioritizes architectural patterns and test-driven development:
- **Event Sourcing** with **Marten** on a single PostgreSQL (events + the `GameState` snapshot)
- **CQRS** — commands mutate one event stream; a Marten `Inline` snapshot is the read model
- **Vertical slices** — one feature = one folder (command + pure decider + Wolverine.HTTP endpoint + response + tests)
- **Real-time multiplayer** — a cascaded notification through the Marten/Wolverine **outbox** → SignalR
- **Comprehensive testing** (domain unit, Alba + TrackedSession integration, bUnit component, Playwright E2E)

> **New here? Read [`docs/critter-stack-onboarding.md`](docs/critter-stack-onboarding.md) first** — slice
> anatomy, the Critter Stack mindset, dev heuristics, and an "add a slice" walkthrough. This file is the
> reference; that doc is the tour.

> **Mobile (HotDice, epic #334):** the stack decision is [ADR 0008](docs/decisions/0008-mobile-via-maui-blazor-hybrid.md)
> (MAUI Blazor Hybrid over a shared RCL; plan in [`docs/mobile-strategy.md`](docs/mobile-strategy.md)); the
> crosscutting practices ported from the reference app mobile app — CI/CD, test tiers, device evidence loop, store
> runbooks, tooling — are inventoried in [`docs/mobile-practices-inventory.md`](docs/mobile-practices-inventory.md)
> with the translation decisions in [ADR 0009](docs/decisions/0009-mobile-practice-port-for-hotdice.md).

### Key Technologies

| Layer | Stack |
|-------|-------|
| Backend API | .NET 10, **Wolverine.HTTP 6.16** (endpoints + message bus + Marten-backed outbox) |
| Event store + read model | **Marten 9.12** on **PostgreSQL** (one store: events + the `GameState` snapshot) |
| Identity DB | PostgreSQL + EF Core (ASP.NET Identity) — the only EF-managed schema |
| Real-time | ASP.NET Core SignalR (`/hubs/game`) |
| OpenAPI | Built-in `Microsoft.AspNetCore.OpenApi` (`/openapi/v1.json`) |
| Frontend | Blazor Server host + Blazor WASM client, MudBlazor, BlazorState |
| API Client | Kiota-generated `Farkle.ApiClient` (shared by WASM client and tests) |
| Auth | JWT bearer (ASP.NET `AddJwtBearer`), ASP.NET Identity |
| Tooling | **JasperFx CLI** (`describe`/`resources`/`codegen`/`projections`), static prod codegen |
| Testing | xUnit, FluentAssertions, **Alba**, Playwright, Testcontainers, bUnit, Moq, AutoFixture |
| Infrastructure | Docker Compose, GitHub Actions CI/CD, CodeQL |

> **.NET version note:** `Directory.Build.props` sets a default `TargetFramework` of `net8.0`, but every project explicitly overrides it to `net10.0`. The repo targets **.NET 10** (`global.json` pins SDK `10.0.0`, roll-forward `feature`). The `verify-generated` CI job installs both 8.0.x and 10.0.x SDKs because the Kiota tooling needs 8.0.

---

## Project Structure

```
src/
├── Farkle/                        # Core: shared aggregate kernel + vertical slices (framework-embracing)
│   ├── Features/                  # ONE FOLDER PER SLICE — command handled by the endpoint, pure decider
│   │   ├── StartGame/  JoinPlayer/  BeginGame/  RollDice/  KeepDice/
│   │   ├── SetDiceAside/  ReturnDice/  PassTurn/  GetGame/  Feedback/
│   │   │                          #   each: <Command>Decider.cs (pure) + <Command>Endpoint.cs (Wolverine.HTTP)
│   │   ├── Responses/             # GameState→DTO mappers (GameStateMapper/LobbyMapper/PassTurnMapper)
│   │   └── GameNotifications.cs   # cascaded outbox notifications (LobbyChanged, GameBegan, …, TurnChanged)
│   ├── Domain/GameAggregate/      # SHARED KERNEL: GameState (Marten snapshot), GameEvents (V1/V2),
│   │                              #   Command, GameValidator, value objects (DieValue, GameId, Score, Player)
│   ├── Application/               # GameCreator, GameNotifier (pushes via IHubContext), GameBroadcastHandler, GameTelemetryHandler, feedback
│   ├── Realtime/                  # GameHub — the SignalR hub lives in the core; GameNotifier broadcasts to it directly (ADR 0005)
│   ├── CritterStackServiceExtensions.cs   # AddFarkleCritterStack (AddMarten + IntegrateWithWolverine + AddWolverine)
│   └── FarkleModuleServiceExtensions.cs   # domain/application DI (IGameCreator, IRandom, GameNotifier, …)
├── Farkle.Shared/                 # Merged dependency-free leaf shared with the WASM client (ADR 0006):
│   │                              #   Contracts/ (HTTP + SignalR DTOs) + Turns/ + Scoring/ (ScoreCalculator, TurnActionPolicy, GameStage)
├── Farkle.Infrastructure/         # Auth/Identity infrastructure only (realtime moved to the core in #323)
│   ├── Identity/                  # AppUser, AppDbContext, Entra data source (AddFarkleIdentity)
│   └── Migrations/                # EF Core Identity migrations (PostgreSQL)
├── Farkle.ApiClient/              # GENERATED Kiota client (do not hand-edit) — shared client
├── WebApp/                        # Blazor Server host (composition root)
│   ├── Internal/Generated/        # GENERATED Wolverine handler/endpoint code (static prod codegen — do not hand-edit)
│   ├── Auth/                      # Register/login minimal-API endpoints + JWT
│   └── Program.cs                 # Composition root (Critter Stack + Wolverine.HTTP + WASM)
├── WebApp.Client/                 # Blazor WASM client
│   ├── Features/                  # BlazorState GameState + Actions/ (Redux-like reducers)
│   ├── Pages/Game/Components/     # Dice tray (tap-to-select), Scoreboard, buttons
│   └── Services/                  # IGameService, IGameHubService, RotationCalculator
└── Blazor.Dice/                   # Reusable CSS-3D dice component (RCL)

infra/                             # Azure Bicep IaC (AVM) — main.bicep + modules + env/*.bicepparam
└── modules/workload.bicep         # Container Apps env, WebApp app, Postgres, Key Vault, ACR, budget

tests/
├── Farkle.Tests/                  # Domain unit + pure decider tests (Features/<Command>/*DeciderShould)
├── Farkle.WebTests/               # Integration (Alba + TrackedSession, Postgres via Testcontainers) — Slices/ per feature
├── Farkle.E2eTests/               # Playwright two-player happy path (+ Storyboard capture)
├── Farkle.SpaTests/               # bUnit component + BlazorState handler tests
├── Blazor.Dice.Tests/             # Dice component rendering tests
└── Farkle.ArchitectureTests/      # ArchUnitNET guardrails (decider purity, slices inward-only)
```

Two solution files exist: **`Farkle.sln`** (full solution — use this) and `src/WebApp.sln` (web-only subset).

---

## Architecture: Event Sourcing, CQRS & vertical slices

> Full detail — with real slice code — is in
> [`docs/critter-stack-onboarding.md`](docs/critter-stack-onboarding.md). This is the summary.

### The request pipeline (one slice)

1. **Endpoint** (`Features/<Command>/<Command>Endpoint.cs`) — a static `[WolverinePost(...)]` method.
   Wolverine loads the aggregate via `[WriteAggregate(FromMethod = nameof(StreamId))] GameState state`
   (`StreamId(int) => $"game-{id}"` maps the int game code to the Marten stream key).
2. **Decider** (`Features/<Command>/<Command>Decider.cs`) — a **pure** `Decide(command, state) → events`
   the endpoint calls directly (commands are *not* dispatched as messages — the endpoint is the handler).
3. **Events** — the endpoint returns a tuple `(Results<Ok<T>, ProblemHttpResult>, Events, GameNotifications.X?)`.
   Wolverine appends `Events` to the stream; the response is built with `GameState.Fold(state, events)`
   (in-memory, no re-read).
4. **Snapshot** (`GameState`) — a Marten **`Inline` self-aggregating snapshot** rebuilt by conventional
   `Create`/`Apply` methods; it *is* both the aggregate and the read model (read-your-own-writes, no daemon).
5. **Broadcast** — if the endpoint returns a `GameNotifications.*`, Wolverine publishes it through the
   Marten **outbox** post-commit → `GameBroadcastHandler` → `GameNotifier` → SignalR (see below).

**Key files:**
- `src/Farkle/Domain/GameAggregate/GameState.cs` — the snapshot: `Create`/`Apply` (Marten replay) **and** a separate pure static `Fold` (deciders/tests/endpoints), plus `Score`, `GameId`, `Player`.
- `src/Farkle/Domain/GameAggregate/GameEvents.cs` — versioned event records (`V1` & `V2`) **only**. Events stay in the shared kernel (they're folded by `GameState` and are the persisted contract) — unlike commands, they do **not** move into slices; an arch test (`DomainPurityShould.NotDependOnTheSlices`) enforces it.
- `IErrorEvent.cs` / `DieValue.cs` / `Dice.cs` / `IRandom.cs` — the marker, the SmartEnum, the dice value object and the RNG seam, each in its own file alongside the events.
- `src/Farkle/Domain/GameAggregate/GameValidator.cs` — validator primitives (`PlayerIsInTurn`, `SingleRoll`, `PlayerCanPass`, …).
- `src/Farkle/Features/<Command>/<Command>Command.cs` — the slice's command record (each slice owns its own).
- `src/Farkle/Domain/GameAggregate/PlayerId.cs` — the player-id value object (shared by the events, `GameState` and every command).
- `src/Farkle/CritterStackServiceExtensions.cs` — `AddFarkleCritterStack` (Marten + Wolverine wiring).

### Validation-as-events

Invalid operations do **not** throw. The decider returns an event implementing **`IErrorEvent`**
(`PlayedOutOfTurn`, `RolledTwice`, `PassedWithoutRolling`, `DieNotAvailableToSetAside`, …). The
endpoint inspects the produced events, maps the **first** `IErrorEvent` to a **400 `ProblemDetails`**
(`TypedResults.Problem(statusCode: 400, title: error.GetType().Name)`), and returns an **empty
`new Events()`** so nothing is appended. Error events have **no `Apply` overload** — they're recorded
as facts but inert on replay. No rule is duplicated in HTTP middleware or the client.

### Event versioning

`V1` and `V2` events coexist. `V2` adds fields (e.g. `PlayerJoined` +Color, `DiceRolled`/`DiceKept`
+`GameStage`). `GameState` has `Apply`/`Fold` handlers for **both**. **Never modify a stored `V1`
schema — add a new `V2` record.**

### Real-time multiplayer (SignalR via the outbox)

```
Features/GameNotifications.cs            # LobbyChanged, GameBegan, DiceRolled, TableChanged, TurnChanged
  → Application/GameBroadcastHandler.cs  # Wolverine Handle(...) per notification (runs after commit)
  → Application/GameNotifier.cs          # reloads the fresh GameState via IQuerySession, then pushes it
  → IHubContext<GameHub> → SignalR group "game-{id}"   # GameHub is in Farkle/Realtime/ (core); no port (ADR 0005)
```

A slice broadcasts by **returning** a `GameNotifications.*` from its endpoint — never by calling the hub
directly. Because it rides the Marten outbox, a broadcast only fires for events that actually committed.
The client (`GameHubService`) listens and dispatches a BlazorState action so other players' UIs update live.

---

## Domain Model Concepts

### Commands (one per slice)
`StartGameCommand`, `JoinPlayerCommand`, `BeginGameCommand`, `RollDiceCommand`, `KeepDiceCommand`,
`SetDiceAsideCommand`, `ReturnDiceCommand`, `PassTurnCommand`. Each lives **in its own slice**
(`Features/<Command>/<Command>Command.cs`) next to the decider and endpoint that use it — a command is
one slice's write-side input, not shared kernel. Value objects the commands are built from (`GameId`,
`PlayerId`, `DieValue`) stay in `Domain/GameAggregate/`.

### Game stages (`Farkle.SharedKernel/Turns/GameStage.cs`)
```csharp
public enum GameStage { None, Rolling, Keeping, Finished, WaitingForPlayers }
```
Persisted **by ordinal** (it's stored on `V2` events / the snapshot as `(int)`), so **only append new
members at the end — never reorder or renumber.**

### Game flow
1. `StartGame` → `GameStarted` (a fresh `game-{id}` stream via `IGameCreator` with id-collision retry)
2. `JoinPlayer` (one per player; ids assigned sequentially) → `BeginGame` once enough players joined
3. `RollDice` → dice appear in the table center (stage → Keeping)
4. `SetDiceAside` / `ReturnDice` → move scoring dice between zones (local selection)
5. `KeepDice` → lock the set-aside dice into the hand, update the turn score
6. `PassTurn` → bank the score, rotate to the next player. Reaching the winning score (`WinningScore`) → `GameWon` (stage → Finished)

### Scoring
Scoring lives in the pure **`Farkle.SharedKernel/Scoring/ScoreCalculator.cs`** (shared by the server
decider *and* the WASM client's live turn-score preview — one source of truth, no duplicated rules).
Tricks include a straight, three-of-a-kind (face × 100), ones/fives, and a full run; the exact
pattern semantics + any combo multipliers live in `ScoreCalculator.cs`. `TurnActionPolicy`
(same project) is the single source of truth for which action (`CanRoll`/`CanKeep`/`CanPass`) is legal
at the current stage — consulted by both `GameValidator` and the client's button gating.

### Key value types (`Domain/GameAggregate/`, one file each)
- **DieValue** (Ardalis SmartEnum, `DieValue.cs`): One…Six (Unicode pip glyphs ⚀⚁⚂⚃⚄⚅), plus `None`
- **PlayerId** (record, `PlayerId.cs`): int wrapper with implicit conversions
- **Player** (record): `(int Id, string Name, string Color)`
- **GameId** (record): int wrapper, `None = new(0)` sentinel
- **Score** (record): int wrapper with implicit conversions
- **GameState** (public record): the Marten snapshot — `Id` (`"game-{code}"`), `Code`, `GameStage`, `Winner`, `TurnScore`, `HasActedThisTurn`, `Players`, `TableCenter`, `DiceKept`, `DiceSetAside`, `TurnNumber`, `ScoreTable`

---

## HTTP API

All game/feedback endpoints are **Wolverine.HTTP** static methods (`Features/<Command>/<Command>Endpoint.cs`),
hosted via `app.MapWolverineEndpoints(...)`. Mutating endpoints take
`[WriteAggregate(FromMethod = nameof(StreamId))] GameState state` and return a tuple
`(Results<Ok<TResponse>, ProblemHttpResult>, Events, GameNotifications.X?)`.

| Endpoint | Route (POST unless noted) | Response |
|----------|---------------------------|----------|
| StartGame | `/api/games` | `StartGameResponse(Id)` |
| JoinPlayer | `/api/games/{gameId}/players` | `JoinPlayerResponse` → `LobbyChanged` |
| BeginGame | `/api/games/{gameId}/start` | → `GameBegan` |
| RollDice | `/api/games/{gameId}/players/{playerId}/rolls` | `RollDiceResponse` → `DiceRolled` |
| SetDiceAside | `/api/games/{gameId}/players/{playerId}/setasides` | `SetAsideResponse` → `TableChanged` |
| ReturnDice | `/api/games/{gameId}/players/{playerId}/putbacks` | → `TableChanged` |
| KeepDice | `/api/games/{gameId}/players/{playerId}/keeps` | `KeepDiceResponse` → `TableChanged` |
| PassTurn | `/api/games/{gameId}/players/{playerId}/turns` | `PassTurnResponse` → `TurnChanged` |
| GetGame | `GET /api/games/{gameId}` | `GameStateResponse` (reads the snapshot; no decider) |
| Feedback | `/api/feedback` | `SubmitFeedbackResponse` (append-only; no decider) |

`PassTurnResponse` carries the full scoreboard (`PlayerScore[]`) + optional winner. All DTOs live in
`src/Farkle.Contracts/HttpRequests.cs` / `HttpResponses.cs`.

**Auth endpoints** (`src/WebApp/Auth/`, **minimal APIs**, anonymous — Identity's `UserManager` isn't
Wolverine-codegen-resolvable, so they're not slices):
- `POST /api/auth/register` — creates an Identity user
- `POST /api/auth/login` — verifies credentials, returns a JWT (`Auth:JwtSecret`, HMAC-SHA256)

Auth is **off by default**; game endpoints are anonymous unless `Auth:RequireAuthorization` is `true`
(then `Program.cs` calls `opts.RequireAuthorizeOnAll()`).

---

## Frontend (Blazor WASM Client)

### State Management (BlazorState)

WASM uses **BlazorState** (a Redux/MediatR-like pattern). `GameState` (`src/WebApp.Client/Features/GameState.cs`) holds game/player IDs, turn score, `CurrentPlayerId` (+ `IsMyTurn`), the live `Scoreboard`, `WinnerName` (+ `IsGameOver`), dice in play, and error/modal UI flags. Mutations happen through **Actions** (each is a nested `Action` record + `Handler`):

| Action | Purpose |
|--------|---------|
| `StartGame` | Calls the API to start a game, stores the game id |
| `JoinPlayer` | Joins the current game, seeds player id/name + scoreboard |
| `RollDiceAction` | Rolls dice; on failure sets error state |
| `SetDiceAside` | Local-only — a tap toggles a die between rolled and set-aside (selected) in the tray |
| `KeepDice` | Sends set-aside dice to the API, updates turn score |
| `PassTurn` | Passes the turn locally (API call), updates scoreboard/winner |
| `RemoteTurnChanged` | Applies a turn change received via SignalR `OnTurnChanged` |
| `LeaveGame` | Resets game state |
| `ToggleErrorModal` | Shows/hides the error modal |

Registered via `services.AddBlazorState(...)` in `ClientServiceExtensions.RegisterClientServices`.

### Services (`src/WebApp.Client/Services/`)
- **`IGameService` / `GameService`** — adapter over the Kiota `FarkleApiClient`; one method per game command. Returns Ardalis `Result<>` where a call can fail. *(This is the **client** service — unrelated to the removed server-side Eventuous `GameService`.)*
- **`IGameHubService` / `GameHubService`** — SignalR client connection (connect/disconnect, `OnTurnChanged`).
- **`IRotationCalculator` / `RotationCalculator`** — maps a `DieValue` to CSS 3D rotation angles `(x, y, z)` for rendering a die face (optional random spin). Registered as a singleton.

### Components (`src/WebApp.Client/Pages/Game/Components/`)
`Game.razor` (route `/games/{gameId:int}`) composes: **Scoreboard** (compact MudSimpleTable, leader highlight, winner banner), **GameDiceTray** — a thin state binding over the reusable **`Blazor.Dice.DiceTray`** (a *tap-to-select* grid: tapping a die toggles it between rolled and set-aside; the library owns the selected visual), each die a **Die** (CSS 3D die using `IRotationCalculator`), **SelectionScore** (live score of the current set-aside selection), **TurnStatus**, **RollDiceButton**, **KeepButton**, **PassTurnButton**, **TurnScore**, **GameTitle**, and the reusable **AppButton**. `Features/Actions/Components/ErrorModal.razor` renders domain errors.

### Game-screen UI conventions & gotchas

These were established/learned while polishing the in-game screen (issue #97) and are easy to break. **Verify any UI change with the storyboard capture at all three viewports** (see Testing Patterns).

- **No-scroll constraint (hard requirement).** Every game screen must fit entirely within the viewport — no vertical or horizontal scroll — at *every* stage (landing, lobby, before/after roll, set-aside, keep, pass, win) and at all three supported sizes: **mobile 390×844, medium 1280×800, large 1920×1080**. `Game.razor` lays the in-play view out as a single flex column (`Game.razor.css`).
- **MudBlazor + component-scoped CSS needs `::deep`.** Blazor scoped `.razor.css` only decorates elements the component renders *directly* — it does **not** reach into child components, so a bare `.mud-button-root` rule silently does nothing against `MudGrid` / `MudButton`. Wrap the MudBlazor markup in a plain element you own (e.g. `<div class="dice-area">`) and target descendants with `::deep` (`.dice-area ::deep .mud-button-root { … }`). Prefer scoped classes / MudBlazor props over inline styles.
- **Dice rendering.** `Die` sizes itself from a `--die-size` custom property; override it on a wrapper (closer than the Die's own `:root`) to resize per breakpoint, and reserve a slot wider than the box (the tilted 3D die overshoots it). On mobile the dice are smaller, laid out **two rows of three** (set the ⅓ width on the `.dice-tray-die` wrapper, not the inner slot), and the `.die.solid` depth body is hidden (it shows as a grey slab at small sizes). Pip margins must scale with `--die-size` (not `vh`) or they overflow the face.
- **Tap-to-select dice tray (#196).** The in-play dice are a single grid (`Blazor.Dice.DiceTray`); tapping a die toggles its `.selected` state (a scale transform + selected face), which replaced the old drag-and-drop "Rolled"/"SetAside" drop zones. The tray is presentational and owns the selected visual; `GameDiceTray` maps a tap to the `SetDiceAside` BlazorState action, and `SelectionScore` shows the live score of the current selection. Binding is guarded by `tests/Farkle.SpaTests/Components/DiceTray/BindingShould.razor`. (On touch this is already native — no HTML5 drag-and-drop, which matters for a future MAUI/mobile port; see [`docs/mobile-strategy.md`](docs/mobile-strategy.md).)
- **Button labels are load-bearing.** The E2E and storyboard tests click by visible text (`button:has-text('Roll' | 'Keep' | 'Pass Turn')`); *set-aside is a tap on a die, not a button*. **Do not rename** these labels — restyle instead (e.g. equalize heights by stretching each button to fill its grid cell; shrink the mobile font to control wrapping).
- **Contrast.** Yellow is the primary colour; set `PrimaryContrastText` (dark) in the theme so text/icons on filled yellow buttons stay legible. Yellow used as *text* on dark backgrounds (titles, scores, game code) is unaffected.

---

## Development Commands

### .NET SDK on Claude Code web / remote sessions
Remote web/cloud containers are ephemeral and ship **without the .NET SDK** — `dotnet` isn't on `PATH`, and the usual `dotnet-install.sh`/CDN hosts are blocked by the network allowlist, so build/test/Kiota all fail until you install it. Install both SDKs via apt from the allowlisted `packages.microsoft.com` (10.0 to build/run, 8.0 for Kiota). Playwright's browser CDN is blocked too — use Microsoft Edge via `PLAYWRIGHT_CHROMIUM_EXECUTABLE_PATH`. **Full runbook (commands, rationale, the SessionStart-hook option, Chromium): [`docs/remote-sessions.md`](docs/remote-sessions.md).** Local dev machines and CI are unaffected.

### Build & Restore
```bash
dotnet restore Farkle.sln
dotnet build Farkle.sln
dotnet build Farkle.sln -c Release
```

### Run Tests

**All tests:** `dotnet test Farkle.sln`

**Single test by name:**
```bash
dotnet test --filter "FullyQualifiedName~GameHappyPath"
dotnet test tests/Farkle.Tests/Farkle.Tests.csproj --filter "FullyQualifiedName~PassTurnDecider"
```

**By category:**
- Domain unit + deciders: `dotnet test tests/Farkle.Tests/Farkle.Tests.csproj`
- Integration (Alba + TrackedSession, needs Postgres): `dotnet test tests/Farkle.WebTests/Farkle.WebTests.csproj`
- Component (bUnit WASM): `dotnet test tests/Farkle.SpaTests/Farkle.SpaTests.csproj`
- E2E (Playwright): `dotnet test tests/Farkle.E2eTests/Farkle.E2eTests.csproj --filter "FullyQualifiedName~GameHappyPath"`

`Farkle.WebTests` boots the real host with **Alba**; provide Postgres via **`FARKLE_TEST_PG`** (a
connection string) or let it spin a **Testcontainers** `postgres:16-alpine`. There is **no EventStore** —
Postgres is the only dependency.

### JasperFx CLI (runs against the real configuration)
```bash
dotnet run --project src/WebApp -- describe            # resolved Marten/Wolverine config
dotnet run --project src/WebApp -- resources list      # endpoints, subscriptions, …
dotnet run --project src/WebApp -- codegen write       # (re)write static handler/endpoint code
dotnet run --project src/WebApp -- codegen test        # compile every handler/endpoint (CI + a WebTest use this)
dotnet run --project src/WebApp -- db-apply            # apply the Marten schema
dotnet run --project src/WebApp -- projections rebuild  # rebuild projections
```

### Run the Application Locally

**Prerequisites:** Docker & Docker Compose (just PostgreSQL now).
```bash
docker-compose up -d                              # PostgreSQL
dotnet run --project src/WebApp/WebApp.csproj     # Blazor Server host (serves the WASM client too)
# Browse to https://localhost:5001 (or http://localhost:5000)
```

**Default seeded user** (created on startup if missing): `player1@email.com` / `Pass@word1`.

**Configuration** (`appsettings.Development.json`): the Marten/Identity Postgres connection (localhost:5432),
and the backend URL the WASM client calls (`BackendUrl`). Marten auto-creates its schema on boot; there's
no EventStore connection string.

### Database Migrations (EF Core — **Identity only**)
Marten manages its own schema (`AutoCreate.CreateOrUpdate`). **Only ASP.NET Identity uses EF migrations:**
```bash
dotnet ef migrations add <Name> -p src/Farkle.Infrastructure/Farkle.Infrastructure.csproj -s src/WebApp/WebApp.csproj
dotnet ef database update  -p src/Farkle.Infrastructure/Farkle.Infrastructure.csproj -s src/WebApp/WebApp.csproj
```
Identity migrations are applied automatically on startup (outside the `NSwag` environment); the app re-seeds the default user.

### Linting & Code Quality
- **.editorconfig** — formatting rules
- **`TreatWarningsAsErrors = true`** (`Directory.Build.props`) — all warnings must be fixed
- **Nullable reference types enabled**
- **CodeQL** SAST runs in CI (`security-and-quality` query suite for C#); generated code is excluded via `.github/codeql/codeql-config.yml`
- No StyleCop/Roslyn analyzer beyond compiler settings

---

## Continuous Integration

### `.github/workflows/e2e-happy-path.yml` (name: **CI - Tests**) — runs on PRs to `main`
1. **`test` (Unit & Integration Tests)** — restores/builds `Farkle.sln`, runs unit → integration (pulls `postgres:16-alpine` for Testcontainers; **no EventStore**) → SPA component tests, coverage to Codecov + TRX artifacts.
2. **`verify-generated`** — regenerates the OpenAPI doc (`-p:GenerateSwagger=true`) and the Kiota client, then **fails if the committed generated files differ**. Installs .NET 8 + 10 + `wasm-tools`.
3. **`verify-codegen`** — regenerates the Wolverine static codegen (`codegen write` in the NSwag env — no DB/WASM) and **fails if `src/WebApp/Internal/Generated` drifts**.
4. **`e2e`** — installs Playwright Chromium, runs `GameHappyPath` (Alice + Bob), uploads videos/screenshots/logs/TRX, parses failures into job outputs.
5. **`deploy-pages`** (`needs: e2e`, `if: always()`) — publishes the E2E videos to GitHub Pages and **upserts** a PR comment (marker `<!-- e2e-video-report -->`) linking `runs/{run_id}/`. Runs `.github/scripts/generate-pages.sh`. **One-time setup:** an admin must enable Pages (branch `gh-pages` / `/`).

### `.github/workflows/storyboard.yml` (name: **CI - Storyboard**) — PRs to `main`
Runs in parallel with `CI - Tests`; runs the storyboard-tagged E2E tests (`--filter "Category=Storyboard"`), captures multi-viewport screenshots, and **upserts** a PR comment (marker `<!-- e2e-storyboard-report -->`) linking `runs/{run_id}/storyboard.html`.

`generate-pages.sh` is **dual-mode** (`MODE=videos` default | `screenshots`); both publishers write into the same `runs/{id}/` tree and share a `concurrency: gh-pages-publish` group.

> **`gh-pages` is kept to a single commit.** Each publisher re-roots the branch to one orphan commit and force-pushes, so the large `.webm`/screenshot blobs never accumulate in history. **Do not** switch these steps back to an incremental `git commit … && git push` — a prior incremental approach grew `.git` to ~4.6 GB of dead video blobs.

### `.github/workflows/codeql.yml` (name: **CI - CodeQL**)
Push/PR to `main` + weekly. Builds the solution and runs CodeQL C# analysis (generated code excluded via `.github/codeql/codeql-config.yml`).

### `.github/workflows/infra-validate.yml` (name: **CI - Infra Validate**) — PRs touching `infra/**`
Builds + lints the Bicep + PSRule, credential-free. This is the gate for infra changes (Bicep can't be linted in remote dev containers).

### Deployment (CD) — two paths
Deploys to Azure run on push to `main` (full runbook in [`infra/OPERATIONS.md`](../infra/OPERATIONS.md)). The line is *resource topology/config vs. just the running app version*:
- **`CD - App Release` (`app-release.yml`)** — the everyday path. A `src/**` change triggers **`CI - Image`** (`build-image.yml`), which builds/pushes `webapp:<sha>` + `:latest` to the persistent ACR, then chains App Release: a fast `az containerapp update` (no ARM, seconds). **Ungated** except `vars.DEPLOY_ENABLED == 'true'`, so its OIDC ids come from **repository** variables, not the gated `production` environment.
- **`CD - Infra Deploy (workload)` (`infra-deploy.yml`)** — full-stack ARM (`az deployment sub create`) for `infra/**` changes and the scheduled re-provision. Deploys the WebApp on `:latest`, so it also rolls the app. Stays **gated** on the `production` environment (required reviewer) and holds the real secrets.

Both share a `concurrency: workload-deploy` lock so they never run at once. Since `:<sha>` and `:latest` are pushed together and full deploys use `:latest`, the two never diverge. **Gotcha:** if a run in that shared lock is *cancelled while queued*, the group can wedge and stop dispatching — cancel the wedged run to clear it. **Budget gotcha:** the Azure consumption budget's `startDate` is pinned in `workload.bicep` (Azure forbids changing it on redeploy); keep it first-of-month.

### Diagnosing E2E Failures
The `e2e` job uploads `e2e-trx-<run_id>` (TRX + stack traces), `e2e-logs-<run_id>`, `e2e-videos-<run_id>` (`.webm`), `e2e-screenshots-<run_id>` (PNGs). **Check the upserted PR comment first** (failing test names + messages + the inline-video Pages link) before downloading artifacts.

---

## Testing Patterns

### Testing Layers — what belongs where

Pick the layer that can answer the question with the least machinery. Overlap is fine **only** when each layer is asserting something the others can't.

| Layer | Project | Owns | Does **not** own | Heuristic |
|---|---|---|---|---|
| **Domain unit / decider** | `Farkle.Tests` | All business rules: deciders (`(command, state) → events`), scoring, state fold, validators. Pure, no I/O. | HTTP, DOM, DI wiring, SignalR. | *"If the test wouldn't change when we swap Wolverine.HTTP for ASP.NET MVC, it belongs here."* |
| **Handler unit (frontend)** | `Farkle.SpaTests/Handlers` | BlazorState `Handler` classes in isolation: mocked client `IGameService`, dispatch the action, assert `GameState` mutation. No bUnit, no DOM. | Business rules (the client is a thin shell). Component rendering. | *"Given a state and a mocked service response, what does the store look like after?"* |
| **Component (bUnit)** | `Farkle.SpaTests/Components` | Rendering, conditional UI (`Disabled`, visibility), event wiring, CSS-class invariants. Mocked `IGameService`/`IGameHubService`. | State-machine internals (handler's job). End-to-end flows (E2E's job). | *"Given this state, does the DOM look right and do clicks fire the right actions?"* |
| **Web integration** | `Farkle.WebTests` | HTTP contract (status/JSON shape), Wolverine.HTTP routing, Marten round-trip, **outbox broadcast** (TrackedSession), Identity/JWT. Real Postgres via Testcontainers. | Exhaustive business rules (in domain unit) — one happy + one representative error path per slice. Frontend rendering. | *"Does the wire format and the wiring still hold together?"* |
| **E2E** | `Farkle.E2eTests` | Real-browser flow: WASM hydration, two-player happy path, SignalR turn flip, CSS layout, win condition. Playwright + real backend + Postgres. | Edge-case business rules; every error path. | *"Can a real user, in a real browser, complete a meaningful journey?"* |

**Anti-patterns to avoid:**
- Re-asserting "can't roll out of turn" in `Farkle.WebTests` — already covered by the decider test. Integration should prove the rejection becomes an HTTP 400 with the right `ProblemDetails`, not re-prove the rule.
- Driving the BlazorState `Sender` inside a bUnit component test to set up state. Handler behaviour → `Handlers/` without bUnit. DOM → set state through the store and render.
- Adding a UI-side copy of a domain rule just to test it. The decider/`TurnActionPolicy` is the source of truth; the client surfaces the resulting error.

### Layer-specific setup

#### Domain / decider tests (`Farkle.Tests`)
Pure decider tests live under `tests/Farkle.Tests/Features/<Command>/*DeciderShould.cs`. They arrange
state with **`GameState.Fold(...events)`** and assert the events `Decide(command, state)` emits — no host,
no mocks beyond `IRandom` where relevant. `Domain/*Should.cs` cover state/scoring/turn rules;
`GameTestAggregate.cs` is a test-only helper that drives `GameState.Fold` like an aggregate.

#### Integration tests (`Farkle.WebTests`) — Alba + TrackedSession
- **Collection fixtures** (`Harness/IntegrationCollections.cs`) — `AppFixture` boots the real `Program`
  once per collection as an **`IAlbaHost`**, on Postgres (`FARKLE_TEST_PG` or a Testcontainer), with a
  distinct Marten schema; `ResetAllData` between tests.
- **`FarkleTestHost`** sets `Auth:RequireAuthorization=true`, points both stores at the test Postgres, and
  **`DisableAllExternalWolverineTransports()`** so cascaded broadcasts stay in-process for tracking.
- **`IntegrationTest`** base drives requests through the generated **Kiota client**; `TrackAsync(...)` =
  `Host.ExecuteAndWaitAsync(...)` (the **TrackedSession** — await outbox side effects deterministically,
  never `Task.Delay`). Assert both the HTTP result and `tracked.Executed.SingleMessage<GameNotifications.X>()`.
- **`Slices/`** — one test class per slice. `CrossCutting/WolverineConfigurationShould.cs` runs
  `codegen test` to compile every handler/endpoint (replaces the retired `AssertWolverineConfigurationIsValid`).

#### E2E tests (`Farkle.E2eTests`)
Playwright drives two browser contexts (Alice + Bob) through the happy path until a win, against a real
host on Postgres (`E2eWebAppFactory` — no ESDB). Records a `.webm` per session; captures structured logs.
Waits for WASM hydration before interacting.

#### Storyboard screenshots (`Farkle.E2eTests`, `Category=Storyboard`)
Multi-viewport screenshots of the opening flow, tagged `[Trait("Category","Storyboard")]`. Deterministic dice
come from a `ScriptedRandom` on the `IRandom` DI seam. Frames land in `test-results/storyboard/{step}-{viewport}.png`.

**The loop for iterating on UI changes locally:**
```bash
dotnet build tests/Farkle.E2eTests/Farkle.E2eTests.csproj
PLAYWRIGHT_CHROMIUM_EXECUTABLE_PATH=/usr/bin/microsoft-edge \
  dotnet test tests/Farkle.E2eTests/Farkle.E2eTests.csproj --no-build --filter "Category=Storyboard"
```
- **Chromium in restricted sandboxes:** the Playwright CDN is blocked; install Edge from `packages.microsoft.com` and point the fixture at it via `PLAYWRIGHT_CHROMIUM_EXECUTABLE_PATH`.
- **No-scroll check:** the capture is full-page, so a frame's PNG height *equal to* the viewport height (mobile 844 / medium 800 / large 1080) means it fits; greater means it scrolls.

#### SPA Tests (`Farkle.SpaTests`)
- **`Components/`** — bUnit component tests (`GameBunitContext` registers MudBlazor, BlazorState, mocked services). DOM + interaction dispatch.
- **`Handlers/`** — plain xUnit against `Handler` classes (`HandlerTestContext`). State-after-action.
- `Services/` covers the client `IGameService` adapter (mocked HTTP). `Architecture/` holds component invariants.

---

## Important Implementation Notes

### Observability / Telemetry (#33, #216, #305)
Telemetry goes to Application Insights via the **Azure Monitor OpenTelemetry distro**, keyed off `APPLICATIONINSIGHTS_CONNECTION_STRING` (injected in Azure from `infra/modules/workload.bicep`); absent locally → console only, **no crash** (`AddFarkleTelemetry` in `src/WebApp/Telemetry/` is a no-op without it).
1. **Traces + metrics** — `UseAzureMonitor(...)` collects HTTP requests, HttpClient + Npgsql dependencies, and the **Marten + Wolverine trace sources and meters**. Wolverine carries the trace context through the outbox, so the async broadcast/telemetry handlers correlate back to the request that produced the event.
2. **Logs** — Serilog owns the console; `UseSerilog(..., writeToProviders: true)` forwards to the OTel logging provider → AI. Domain-event logs (from `GameTelemetryHandler`, tagged `EventType`) are promoted to AI **`customEvents`** by `DomainEventLogProcessor`.
3. **Browser/UI** — the AI JavaScript SDK is rendered into `src/WebApp/Components/App.razor` when the connection string is present.

`GameTelemetryHandler` (`src/Farkle/Application/`) is a Wolverine handler on the committed notifications; the pure `GameTelemetry.Log` shape is unit-tested. **Always use structured properties** (`{gameId}`, `{playerId}`, `{@GameEvent}`) — never string interpolation — and **never log passwords or tokens**.

### Critter Stack registration (`AddFarkleCritterStack`)
`src/Farkle/CritterStackServiceExtensions.cs` wires the stack:
```csharp
services.AddMarten(opts =>
{
  opts.Connection(connectionString);
  opts.Events.StreamIdentity = StreamIdentity.AsString;             // "game-{code}" (ADR 0002)
  opts.Projections.Snapshot<GameState>(SnapshotLifecycle.Inline);   // self-aggregating read model, no daemon
  opts.UseSystemTextJsonForSerialization(o => o.Converters.Add(new SmartEnumValueConverter<DieValue,int>()));
}).IntegrateWithWolverine();                                        // Marten backs the Wolverine outbox

services.AddWolverine(opts =>
{
  opts.Policies.AutoApplyTransactions();
  opts.Discovery.IncludeAssembly(typeof(CritterStackServiceExtensions).Assembly);
  if (lightweight) opts.Durability.Mode = DurabilityMode.MediatorOnly;   // NSwag: boot without a live DB
});
```
`GameState` is an **`Inline`** snapshot (read-your-own-writes) — there is **no async daemon**. New domain
services go in `FarkleModuleServiceExtensions` (`IGameCreator`, `IRandom`, `GameNotifier`, `IFeedbackWriter`);
`SetUpFarkleModule()` is now a no-op (the Eventuous `TypeMap` bootstrap is gone).

### Optimistic-concurrency retry (#310)
Concurrent writes to the same `game-{id}` stream trip Marten's optimistic-concurrency check
(`JasperFx.ConcurrencyException`). **Wolverine's `OnException`/`RetryWithCooldown` retry policies do NOT
apply to Wolverine.HTTP endpoints** — only to the message bus. So the retry is a small ASP.NET
middleware in `Program.cs` that catches the exception and re-executes the request (short backoff, body
buffered, guarded by `!Response.HasStarted`); the retried endpoint re-fetches the advanced stream and
re-runs the decider. See ADR 0004's concurrency note; guarded by `CrossCutting/ConcurrencyShould.cs`.

### Wolverine.HTTP + codegen
`Program.cs` calls `AddWolverineHttp()` and `MapWolverineEndpoints(...)`. `AddJasperFx(...)` sets
`opts.ApplicationAssembly = typeof(Program).Assembly` (the committed generated code lives in the **WebApp**
host assembly, not `Farkle`), Production → `TypeLoadMode.Static` (+ `AssertAllPreGeneratedTypesExist`),
Development/tests/NSwag → `Dynamic`. The host entrypoint is `return await app.RunJasperFxCommands(args);`.
`ConfigureHttpJsonOptions(... NumberHandling = Strict)` keeps ints typed so the OpenAPI doc → Kiota stays correct.

### NSwag environment (OpenAPI extraction)
`verify-generated` boots the host in the **`NSwag`** environment with `GenerateSwagger=true` to extract the
OpenAPI doc. There, `AddFarkleCritterStack(lightweight: true)` → Wolverine mediator-only + lazy Marten, so it
boots **without a live database**; codegen stays `Dynamic`. Don't break the `IsEnvironment("NSwag")` guards.

### Pinned / notable versions
- **Marten** `9.12.0`, **Wolverine** `6.16.0` (+ `Wolverine.Http`, `Wolverine.Marten`) — the Critter Stack.
- **Kiota** `1.31.1` (pinned in `.config/dotnet-tools.json`) — the deliberately-pinned tool; needs the .NET 8 SDK.
- OpenAPI is the **built-in** `Microsoft.AspNetCore.OpenApi` (NSwag/Swashbuckle removed from the runtime path; `Microsoft.OpenApi` is pinned for a CVE).

---

## Generated Files Policy

**Never hand-edit these files** — they are auto-generated and any manual changes will be overwritten:

- `src/WebApp.Client/swagger.json` — the OpenAPI doc, generated from the ASP.NET app (built-in `Microsoft.AspNetCore.OpenApi`, extracted via `GetDocument` with `-p:GenerateSwagger=true`)
- `src/Farkle.ApiClient/**` — generated by Kiota from `swagger.json` (single shared client used by both `WebApp.Client` and `Farkle.WebTests`)
- `src/WebApp/Internal/Generated/**` — JasperFx/Wolverine pre-generated handler + HTTP-endpoint code (#305). Production loads it via `TypeLoadMode.Static` for a fast cold start; dev/tests/NSwag regenerate in-memory (`Dynamic`).

After any API-contract change (edit a DTO in `src/Farkle.Contracts/` or a route), **regenerate `swagger.json` + the Kiota client and commit** — CI's `verify-generated` fails on drift. Commands: [`docs/api-client-generation.md`](docs/api-client-generation.md).

After changing a Wolverine handler or endpoint (adding/removing a slice, changing a signature), **regenerate the codegen and commit**: `dotnet run --project src/WebApp --no-launch-profile -- codegen write` (runs in the NSwag env — no database). CI's `verify-codegen` fails if `src/WebApp/Internal/Generated` drifts.

---

## Common Workflows

### Adding a slice (a new game command)
See the end-to-end walkthrough in
[`docs/critter-stack-onboarding.md` §7](docs/critter-stack-onboarding.md). In short:
1. `Features/<Command>/` folder; add the command record as `Features/<Command>/<Command>Command.cs`.
2. Add the event(s) to `GameEvents.cs` + a `Handle`/`Apply`/`Fold` case in `GameState.cs`.
3. **Decider test first** (`tests/Farkle.Tests/Features/<Command>/`), then the pure `Decide`.
4. `<Command>Endpoint.cs` — `[WolverinePost]` + `[WriteAggregate(FromMethod = nameof(StreamId))]`, error→400, tuple return.
5. Response DTO in `Farkle.Contracts` + a mapper under `Features/Responses/`.
6. Integration test in `tests/Farkle.WebTests/Slices/` (use `TrackAsync` if it broadcasts).
7. Regenerate: `codegen write`; and if the contract changed, `swagger.json` + the Kiota client.

### Adding validation to a slice
Add a `Validator` primitive in `GameValidator.cs`, compose it with `.And()` in the decider, and on failure
return an `IErrorEvent` — the endpoint maps the first one to a 400 `ProblemDetails`.

### Extending game state
Add a property to the `GameState` record, a `Handle<Event>` static, **and** wire it in **both** the Marten
convention (`Create`/`Apply(<Event>)`) **and** the pure `Fold` switch, so replay and deciders/tests agree.

### Adding/changing real-time updates
Add a record to `Features/GameNotifications.cs`, return it from the slice endpoint, add a `Handle(...)` in
`GameBroadcastHandler`, have `GameNotifier` push the new message through `IHubContext<GameHub>`, and add the
client `.On<T>("MessageName", …)` listener + BlazorState action.

---

## Troubleshooting

### E2E Tests Time Out on WASM Hydration
- Ensure Postgres is up: `docker-compose ps`
- Check the browser console / `e2e-logs` artifact for JS errors
- Slow machines may need a longer hydration wait in the test

### Marten / Postgres connection failed
Verify the Postgres connection string in `appsettings.Development.json` and that Postgres is running
(`docker-compose up -d`). Marten auto-creates its schema on boot; there's no separate event-store service.
Inspect the resolved config with `dotnet run --project src/WebApp -- describe`.

### Identity migration issues
```bash
dotnet ef database drop   -p src/Farkle.Infrastructure/Farkle.Infrastructure.csproj -s src/WebApp/WebApp.csproj -f
dotnet ef database update -p src/Farkle.Infrastructure/Farkle.Infrastructure.csproj -s src/WebApp/WebApp.csproj
```
The app re-seeds `player1@email.com` on startup if missing. (Only Identity uses EF migrations; Marten schema is self-managed.)

---

## Contributing & PR Standards

- **Warnings as errors**: all compiler warnings must be resolved.
- **Event versioning**: never modify a stored V1 event; create a V2 with the new schema.
- **Generated files**: regenerate + commit `swagger.json` + `Farkle.ApiClient/` after a contract change, and `Internal/Generated` after a handler/endpoint change (CI's `verify-generated` + `verify-codegen` enforce both).
- **Decider purity**: keep `Decide` free of Marten/Wolverine/ASP.NET/Npgsql — the arch-test enforces it.
- **Slice isolation**: a slice may use the shared kernel + application layer, but must not reach another slice, Infrastructure, or the host.
- **Test coverage**: new domain logic needs a decider/unit test; new slices need an integration test; new features need at least one E2E happy-path test.
- **UI changes**: verify with the storyboard capture and keep the no-scroll constraint at all three viewports; style via component-scoped CSS (`::deep` for MudBlazor children), not inline styles, and don't rename button labels the tests select by text.
- **Evidence the change**: close every user-facing change with visual proof of the real thing running, attached to the PR — the storyboard frames today (mobile screenshots/video once #339 lands). Actually open the artifact and confirm the change is visible; a green test with a blank recording proves nothing.
- **Close issues via PR**: every PR that resolves an issue MUST include `Closes #<issue>` (or `Fixes #<issue>`) in the body. A bare `#N` is not enough — the keyword is required.
- **TDD commit convention** (Red–Green):
  - **Commit 1 (Red):** failing tests only — must fail before the fix.
  - **Commit 2+ (Green):** implementation that makes them pass. Never mix test and fix changes in one commit.
- **Test-first on touched code**: before modifying *existing* code, add the missing tests that characterize its current behaviour and get them green **on the existing code** — then make the change. This protects what you touch, not only what you add. New code still follows the normal Red→Green.
- **Fixing a bug: reproduce first**: drive every defect from a failing test through a real entry point, confirm it fails for the *actual* reason, apply the smallest fix, and keep the reproduction as a permanent regression test. Never patch blind.
- **PR template**: `.github/pull_request_template.md` renders this list as the Definition of Done on every PR. Leave a box unticked with a one-line note rather than ticking it optimistically.
- **Report faithfully**: if a test failed, say so with the output; if a step was skipped, say that. A PR's Verification section describes what was actually exercised — never "tests pass" when they weren't run.

### Test hygiene

- **Deterministic only** — no `DateTime.Now`, `Random`, or `Thread.Sleep` in a test; use the injected `TimeProvider`/`IRandom` seam and poll rather than sleep. A flaky test is a broken test.
- **One behaviour per test**, arrange-act-assert, named `Method_State_ExpectedResult` (or the `…Should` convention the existing suites use).
- **Builders over inline setup** (AutoFixture/harnesses already in `tests/`), and **no shared mutable state** between tests.
- **Pick the lowest layer that can catch the bug** — reserve the slower layers for what only they can prove (see [Testing Patterns](#testing-patterns)).

### Autonomy boundaries (for AI agents)

Proceed without asking on reversible, in-scope work. **Stop and ask first** before:

- database or event-schema changes (including a new stored event version),
- exposing a new public HTTP endpoint or changing an existing contract,
- adding a dependency (NuGet/npm) or upgrading a pinned one,
- destructive or irreversible operations — data deletes, force-push, rewriting history, infra teardown,
- anything touching auth, security, or secrets.

When in doubt, do the reversible part and ask about the rest — don't stall the whole task on one question.

---

## Agent Workflow for Features

> Repo-scoped commands in `.claude/commands/` encode this workflow: **`/new-story <issue#> <description>`**
> (branch from fresh `origin/main`, restate the acceptance criteria, record `Closes #N`) and **`/pr`**
> (review the diff, fill the Definition-of-Done template, open and subscribe).

### Starting a New Feature
Always sync with the latest `main` first, then branch from it:
```bash
git checkout main && git pull origin main
```
Never start new work from a stale or unrelated branch. Branch naming: `feature/<issue#>-<kebab-description>`, so `/pr` can derive the `Closes #N` link.

### PR & CI Loop
1. **Write the decider/E2E tests** for the happy path before/alongside the implementation.
2. **Open a PR** targeting `main` once the feature and tests are committed.
3. **Subscribe to PR activity** with `subscribe_pr_activity` immediately after opening so CI results and review comments arrive automatically — do not poll.
4. **Wait for CI**. The relevant signals are the `test`, `verify-codegen`/`verify-generated`, and `e2e` jobs:
   - Pass → wait for the PR to be **merged** before starting the next feature.
   - Fail → diagnose from the PR comment / artifacts and push a fix.

### 5-Commit Limit
If E2E tests are still failing after **5 commits** on the PR branch: close the PR, delete the branch, open a new issue (or comment on the originating one) summarizing the intended implementation, each approach tried and why it failed (with TRX/log error messages), and the still-failing test(s) with the last failure reason — then stop and leave the summary for a fresh start.
