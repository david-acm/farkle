# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Repository Overview

**Farkle** is a sample .NET application demonstrating Event Sourcing, CQRS, Clean Architecture, and Event Modelling patterns. It implements the Greedy/Farkle dice game as a backend API (WebApi) and a frontend (Blazor Server host + WASM client) with **real-time multiplayer** over SignalR.

The codebase prioritizes architectural patterns and test-driven development:
- **Event Sourcing** with the Eventuous framework and EventStore (ESDB)
- **CQRS** separation of commands and queries
- **Domain-Driven Design** with an aggregate root and validation
- **Real-time multiplayer** via a SignalR hub broadcasting turn changes
- **Comprehensive testing** (unit, integration, component, E2E with Playwright)

### Key Technologies

| Layer | Stack |
|-------|-------|
| Backend API | .NET 10, FastEndpoints 5.x, Eventuous 0.15.0-beta |
| Event Store | EventStore DB (ESDB) via `EventStore.Client.Grpc` |
| Identity DB | PostgreSQL + EF Core (ASP.NET Identity) |
| Real-time | ASP.NET Core SignalR (`/hubs/game`) |
| Frontend | Blazor Server host + Blazor WASM client, MudBlazor, BlazorState |
| API Client | Kiota-generated `Farkle.ApiClient` (shared by WASM client and tests) |
| Auth | JWT bearer (FastEndpoints.Security), ASP.NET Identity |
| Testing | xUnit, FluentAssertions, Playwright, Testcontainers, bUnit, Moq, AutoFixture |
| Infrastructure | Docker Compose, GitHub Actions CI/CD, CodeQL |

> **.NET version note:** `Directory.Build.props` sets a default `TargetFramework` of `net8.0`, but every project explicitly overrides it to `net10.0`. The repo targets **.NET 10** (`global.json` pins SDK `10.0.0`, roll-forward `feature`). The `verify-generated` CI job installs both 8.0.x and 10.0.x SDKs because the Kiota tooling needs 8.0.

---

## Project Structure

```
src/
├── Farkle/                    # Core domain + application + endpoints (Event Sourcing)
│   ├── Domain/GameAggregate/  # Aggregate root, events, validators, scoring
│   ├── Application/           # GameService (command service), IGameEventBroadcaster
│   ├── Endpoints/             # FastEndpoints (StartGame, RollDice, KeepDice, PassTurn, JoinPlayer)
│   └── FarkleModuleServiceExtensions.cs  # DI registration + Eventuous/ESDB setup
├── Farkle.Contracts/          # HTTP request/response DTOs (no dependencies)
├── Farkle.SharedKernel/       # Shared utilities (Result extensions, TypedEndpoint base)
├── Farkle.ApiClient/          # GENERATED Kiota client (do not hand-edit) — shared client
├── WebApp/                    # Blazor Server host
│   ├── Auth/                  # Identity (AppUser, AppDbContext), register/login endpoints (JWT)
│   ├── Hubs/                  # GameHub + SignalRGameEventBroadcaster (real-time turn updates)
│   ├── Migrations/            # EF Core Identity migrations (PostgreSQL)
│   └── Program.cs             # Composition root (wires Farkle module, SignalR, Identity, WASM)
├── WebApp.Client/             # Blazor WASM client
│   ├── Features/              # BlazorState GameState + Actions/ (Redux-like reducers)
│   ├── Pages/Game/Components/ # Dice, Scoreboard, buttons, drag-and-drop UI
│   └── Services/              # IGameService, IGameHubService, RotationCalculator

infra/                         # Azure Bicep IaC (AVM) — main.bicep + modules + env/*.bicepparam
└── modules/workload.bicep     # Container Apps env, ESDB + WebApp apps, Postgres, Key Vault, ACR

tests/
├── Farkle.Tests/              # Unit tests for the domain (Game, validators, state)
├── Farkle.WebTests/           # Integration tests (API + SignalR via WebApplicationFactory + Testcontainers)
├── Farkle.E2eTests/           # End-to-end tests (Playwright, two-player happy path, video + screenshots)
└── Farkle.SpaTests/           # Component tests (bUnit) for WASM components
```

Two solution files exist: **`Farkle.sln`** (full solution — use this) and `src/WebApp.sln` (web-only subset).

---

## Architecture: Event Sourcing & CQRS

### Event Sourcing Pipeline

All game state mutations are captured as immutable events persisted to EventStore:

1. **Commands** (`Command.*`) → HTTP request mapped at a FastEndpoint
2. **Aggregate** (`Game`) → applies the command, emits event(s)
3. **Validators** (`GameValidator`) → pre-conditions checked in `Game.Apply(...)` before the event is stored
4. **Events** (`GameEvents.V1.*`, `V2.*`) → immutable facts stored in ESDB
5. **State** (`GameState`) → rebuilt by replaying events via `On<EventType>()` handlers

**Key files:**
- `src/Farkle/Domain/GameAggregate/Game.cs` — aggregate root, command handlers, scoring (`GetNewTurnScore`)
- `src/Farkle/Domain/GameAggregate/GameState.cs` — immutable state, event-replay handlers
- `src/Farkle/Domain/GameAggregate/GameValidator.cs` — pre-condition + scoring validators
- `src/Farkle/Domain/GameAggregate/GameEvents.cs` — versioned event records (V1 & V2)
- `src/Farkle/Domain/GameAggregate/Command.cs` — command records

### Validation-as-Events

Invalid operations do **not** throw to the caller. `Game.Apply(object @event)` runs `GameValidator.ValidatePreconditions(this, @event)`; on failure it applies a **failed-validation error event** (implementing `IErrorEvent`) instead of the intended event. The application layer detects `IErrorEvent` instances in `Changes` and converts them to HTTP errors. Error events include `PlayedOutOfTurn`, `RolledTwice`, `PassedWithoutRolling`, and `DiceNotAllowedToBeKept`.

### Event Versioning

Two event versions coexist:
- **V1** events: original `DiceRolled` / `DiceKept` without game-stage tracking, plus `GameStarted`, `PlayerJoined`, `TurnPassed`, `GameWon`, and the error events.
- **V2** events: `DiceRolled` and `DiceKept` carry an extra `GameStage` field for clearer state-machine semantics.

`GameState` registers handlers for both versions. The aggregate uses **V2 for rolling** (`RollDiceV2`) going forward. Never modify a V1 event schema — add a new version instead.

### Command Service & Validation Composition

`GameService` (extends `Eventuous.CommandService<Game, GameState, GameId>`) orchestrates:
- Command routing via fluent builder: `.On<Command.StartGame>().InState(New).Execute(...)`
- Aggregate load/save via Eventuous + `EsdbEventStore`
- `CommandHandlerBuilderExtensions.Execute(...)` wraps handlers in a try/catch for `DomainException` so error events still persist instead of failing the whole operation.
- `IGameService.HandleAsync<TCommand, TResponse>(cmd, ct, mapper)` runs the command, scans `Changes` for `IErrorEvent`, and either returns an HTTP error or maps `GameState` → the endpoint's response DTO.

**Validators** use the **Composite Pattern** with a fluent `.And()` builder:
```csharp
new PlayerIsInTurn(state, e.PlayerId)
  .And(new SingleRoll(state, e.PlayerId))
  .IsSatisfied()
```
`AndValidator` short-circuits on the first failing validator.

### Real-Time Multiplayer (SignalR)

Turn changes are pushed to all players in a game in real time:

1. **Server hub** — `src/WebApp/Hubs/GameHub.cs` exposes `JoinGame(int gameId)` / `LeaveGame(int gameId)`, which add/remove the connection to the SignalR group `game-{gameId}`. Mapped at `/hubs/game` in `Program.cs`.
2. **Broadcaster** — `IGameEventBroadcaster` (in `src/Farkle/Application/`) is implemented by `SignalRGameEventBroadcaster` (in `src/WebApp/Hubs/`), registered scoped in `Program.cs`. Its `BroadcastTurnChangedAsync(PassTurnResponse, ct)` sends the `"TurnChanged"` message (carrying a `PassTurnResponse`) to group `game-{GameId}`.
3. **Trigger** — `PassTurnEndpoint` injects `IGameEventBroadcaster` and, after a successful pass, broadcasts the turn change (broadcast failures are logged, not propagated to the HTTP response).
4. **Client** — `IGameHubService` / `GameHubService` (in `src/WebApp.Client/Services/`) builds an auto-reconnecting `HubConnection`, registers `.On<PassTurnResponse>("TurnChanged", ...)`, calls `JoinGame`, and raises `OnTurnChanged`. `Game.razor.cs` subscribes and dispatches the `RemoteTurnChanged` BlazorState action so other players' UIs update live.

---

## Domain Model Concepts

### Commands (`Command.cs`)
`StartGame`, `JoinPlayer`, `RollDice`, `KeepDice`, `PassTurn` (plus the `PlayerId` value object).

### Game Stages
```csharp
internal enum GameStage { None, Rolling, Keeping, Finished }
```

### Game Flow
1. `StartGame` → `GameStarted` (stage = Rolling)
2. `JoinPlayer` (one per player; player IDs assigned sequentially)
3. `RollDice` → dice appear in the table center (`RollDiceV2` → stage = Keeping)
4. `KeepDice` → move scoring dice to hand, update turn score
5. `PassTurn` → lock in score, rotate to next player. If score ≥ **10,000** → `GameWon` (stage = Finished)

### Score Calculation (`Game.GetNewTurnScore`)
Scoring iterates a priority-ordered set of validators and takes the first satisfied trick:

| Trick (validator) | Points |
|-------------------|--------|
| `DiceAreStraight` | 1,000 |
| `DiceAreTrips` (three of a kind) | face value × 100 (e.g. three 4s = 400) |
| `DiceAreOnesOrFives` | 100 per `1` + 50 per `5` |
| `DiceAreStair` (full 1-2-3-4-5-6) | 1,500 |

**Combo multiplier:** if the kept dice form a straight **and** at least one straight was already kept this turn (`StraightsKeptThisTurn > 0`), the running total is doubled: `(currentScore + turnScore) * 2`. Exact dice-pattern semantics for each validator live in `GameValidator.cs`.

### Key Value Types
- **DieValue** (Ardalis SmartEnum): One…Six (with Unicode pip glyphs ⚀⚁⚂⚃⚄⚅), plus `None`
- **Player** (record): `(int Id, string Name)`
- **GameId** (extends `Eventuous.Id`): implicit conversions to/from `int`
- **Score** (record): int wrapper with implicit conversions
- **Dice**: value object over a collection of `DieValue`
- **GameState** (record): immutable; `Players`, `TableCenter`, `DiceKept`, `TurnScore`, `ScoreTable`, `StraightsKeptThisTurn`, `Winner`, `GameStage`

---

## HTTP API

All game endpoints are FastEndpoints extending `TypedEndpoint<TRequest, TResponse>` (base in `Farkle.SharedKernel`). They inject `ILogger` + `IGameService` and call `service.HandleAsync<Command, Response>(cmd, ct, mapper)`.

| Endpoint | Route (POST) | Request → Response |
|----------|--------------|--------------------|
| StartGame | `/api/games` | `StartGameRequest(Id)` → `StartGameResponse(Id)` |
| JoinPlayer | `/api/games/{gameId}/players` | `JoinPlayerRequest(GameId, PlayerName)` → `JoinPlayerResponse(Id, CurrentPlayerId)` |
| RollDice | `/api/games/{gameId}/players/{playerId}/rolls` | `RollDiceRequest(GameId, PlayerId)` → `RollDiceResponse(Id, DiceValues)` |
| KeepDice | `/api/games/{gameId}/players/{playerId}/keeps` | `KeepDiceRequest(GameId, PlayerId, DiceValues)` → `KeepDiceResponse(Id, TurnScore)` |
| PassTurn | `/api/games/{gameId}/players/{playerId}/turns` | `PassTurnHttp(GameId, PlayerId)` → `PassTurnResponse(...)` (also broadcast over SignalR) |

`PassTurnResponse` also carries the full scoreboard (`PlayerScore[]`) + optional winner and is broadcast over SignalR. All DTOs live in `src/Farkle.Contracts/HttpRequests.cs` / `HttpResponses.cs`.

**Auth endpoints** (`src/WebApp/Auth/`, FastEndpoints, `AllowAnonymous`):
- `POST /api/auth/register` — creates an Identity user
- `POST /api/auth/login` — verifies credentials, returns a JWT (`Auth:JwtSecret`, 4-hour expiry)

Auth is **off by default**; game endpoints `AllowAnonymous` unless `Auth:RequireAuthorization` is `true` in config (see `Program.cs`).

---

## Frontend (Blazor WASM Client)

### State Management (BlazorState)

WASM uses **BlazorState** (a Redux/MediatR-like pattern). `GameState` (`src/WebApp.Client/Features/GameState.cs`) holds game/player IDs, turn score, `CurrentPlayerId` (+ `IsMyTurn`), the live `Scoreboard`, `WinnerName` (+ `IsGameOver`), dice in play, and error/modal UI flags. Mutations happen through **Actions** (each is a nested `Action` record + `Handler`):

| Action | Purpose |
|--------|---------|
| `StartGame` | Calls the API to start a game, stores the game id |
| `JoinPlayer` | Joins the current game, seeds player id/name + scoreboard |
| `RollDiceAction` | Rolls dice; on failure sets error state |
| `SetDiceAside` | Local-only — toggles a die between "Rolled" and "SetAside" zones (drag-and-drop) |
| `KeepDice` | Sends set-aside dice to the API, updates turn score |
| `PassTurn` | Passes the turn locally (API call), updates scoreboard/winner |
| `RemoteTurnChanged` | Applies a turn change received via SignalR `OnTurnChanged` |
| `LeaveGame` | Resets game state |
| `ToggleErrorModal` | Shows/hides the error modal |

Registered via `services.AddBlazorState(...)` in `ClientServiceExtensions.RegisterClientServices`.

### Services (`src/WebApp.Client/Services/`)
- **`IGameService` / `GameService`** — adapter over the Kiota `FarkleApiClient`; one method per game command (`StartGameAsync`, `JoinPlayerAsync`, `RollDiceAsync`, `KeepDiceAsync`, `PassTurnAsync`). Returns Ardalis `Result<>` where a roll can fail.
- **`IGameHubService` / `GameHubService`** — SignalR client connection (connect/disconnect, `OnTurnChanged`).
- **`IRotationCalculator` / `RotationCalculator`** — maps a `DieValue` to CSS 3D rotation angles `(x, y, z)` for rendering a die face (optional random spin). Registered as a singleton.

### Components (`src/WebApp.Client/Pages/Game/Components/`)
`Game.razor` (route `/games/{gameId:int}`) composes: **Scoreboard** (compact MudSimpleTable, leader highlight, winner banner), **DragabbleDice** (MudDropContainer with stable-height "Rolled"/"SetAside" zones and floating titles), **Die** (CSS 3D die using `IRotationCalculator`), **RollDiceButton**, **KeepButton**, **PassTurnButton**, **TurnScore**, **GameTitle**, and the reusable **AppButton**. `Features/Actions/Components/ErrorModal.razor` renders domain errors.

### Game-screen UI conventions & gotchas

These were established/learned while polishing the in-game screen (issue #97) and are easy to break. **Verify any UI change with the storyboard capture at all three viewports** (see Testing Patterns).

- **No-scroll constraint (hard requirement).** Every game screen must fit entirely within the viewport — no vertical or horizontal scroll — at *every* stage (landing, lobby, before/after roll, set-aside, keep, pass, win) and at all three supported sizes: **mobile 390×844, medium 1280×800, large 1920×1080**. `Game.razor` lays the in-play view out as a single flex column (`Game.razor.css`).
- **MudBlazor + component-scoped CSS needs `::deep`.** Blazor scoped `.razor.css` only decorates elements the component renders *directly* — it does **not** reach into child components, so a bare `.zone` / `.mud-button-root` rule silently does nothing against `MudDropZone` / `MudGrid` / `MudButton`. Wrap the MudBlazor markup in a plain element you own (e.g. `<div class="dice-area">`) and target descendants with `::deep` (`.dice-area ::deep .zone { … }`). Prefer scoped classes / MudBlazor props over inline styles.
- **Dice rendering.** `Die` sizes itself from a `--die-size` custom property; override it on a wrapper (closer than the Die's own `:root`) to resize per breakpoint, and reserve a slot wider than the box (the tilted 3D die overshoots it). On mobile the dice are smaller, laid out **two rows of three** (set the ⅓ width on the `.mud-drop-item` wrapper, not the inner slot), and the `.die.solid` depth body is hidden (it shows as a grey slab at small sizes). Pip margins must scale with `--die-size` (not `vh`) or they overflow the face.
- **Stable dice-zone height (no flicker).** Drop zones reserve a fixed `--zone-height`, identical empty vs. full — a content-driven height made them resize on every action. Guarded by `tests/Farkle.SpaTests/Components/DragabbleDice/StableZoneHeightShould.razor`. Flex pitfall: `flex: 1 1 0` makes `height` the *main-size* in a column layout and collapses the zone — keep the fixed height and only apply `flex-grow` in the side-by-side (row) layout.
- **Button labels are load-bearing.** The E2E and storyboard tests click by visible text (`button:has-text('Roll' | 'Set Dice Aside' | 'Pass Turn')`). **Do not rename** these labels — restyle instead (e.g. equalize heights by stretching each button to fill its grid cell; shrink the mobile font to control wrapping).
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

**All tests:**
```bash
dotnet test Farkle.sln
```

**Single test by name:**
```bash
dotnet test --filter "FullyQualifiedName~GameHappyPath"
dotnet test tests/Farkle.Tests/Farkle.Tests.csproj --filter "FullyQualifiedName~RollShould"
```

**By category:**
- Unit: `dotnet test tests/Farkle.Tests/Farkle.Tests.csproj`
- Integration (API + SignalR): `dotnet test tests/Farkle.WebTests/Farkle.WebTests.csproj`
- Component (bUnit WASM): `dotnet test tests/Farkle.SpaTests/Farkle.SpaTests.csproj`
- E2E (Playwright): `dotnet test tests/Farkle.E2eTests/Farkle.E2eTests.csproj --filter "FullyQualifiedName~GameHappyPath"`

**E2E artifacts** are written to `test-results/`: videos (`videos/*.webm`), screenshots (`screenshots/*.png`), logs (`logs/`), and TRX results — all uploaded as GitHub artifacts in CI.

### Run the Application Locally

**Prerequisites:** Docker & Docker Compose.
```bash
# Start dependencies (PostgreSQL + EventStore)
docker-compose up -d

# Run the Blazor Server host (serves the WASM client too)
dotnet run --project src/WebApp/WebApp.csproj

# Browse to https://localhost:5001 (or http://localhost:5000)
```

**Default seeded user** (created on startup if missing):
- Email: `player1@email.com`
- Password: `Pass@word1`

**Configuration** (`appsettings.Development.json`):
- EventStore: `esdb://admin:changeit@localhost:2113?tls=false`
- PostgreSQL (Identity): `farkle_identity` on localhost:5432
- Backend URL the WASM client calls: `http://localhost:5157` (`BackendUrl`)

### Database Migrations (EF Core / PostgreSQL Identity)
```bash
dotnet ef migrations add <MigrationName> -p src/WebApp/WebApp.csproj
dotnet ef database update -p src/WebApp/WebApp.csproj
```
Migrations are applied automatically on startup (outside the `NSwag` environment); the app also re-seeds the default user.

### Linting & Code Quality
- **.editorconfig** — formatting rules
- **`TreatWarningsAsErrors = true`** (`Directory.Build.props`) — all warnings must be fixed
- **Nullable reference types enabled**
- **CodeQL** SAST runs in CI (`security-and-quality` query suite for C#)
- No StyleCop/Roslyn analyzer beyond compiler settings

---

## Continuous Integration

### `.github/workflows/e2e-happy-path.yml` (name: **CI - Tests**) — runs on PRs to `main`
Four jobs:
1. **`test` (Unit & Integration Tests)** — restores/builds `Farkle.sln`, runs unit → integration (pulls `eventstore:23.10.0` + `postgres:16-alpine` for Testcontainers) → SPA component tests, with coverage uploaded to Codecov and TRX artifacts.
2. **`verify-generated`** — regenerates `swagger.json` (`-p:GenerateSwagger=true`) and the Kiota client, then **fails if the committed generated files differ**. Installs both .NET 8 and 10 SDKs + `wasm-tools`.
3. **`e2e`** — installs Playwright Chromium, runs the `GameHappyPath` E2E test (two players, Alice + Bob), and uploads videos/screenshots/logs/TRX. On failure it parses failing test names + messages from the TRX into job outputs (`fail_names`/`fail_msgs`) for the `deploy-pages` job to surface.
4. **`deploy-pages`** (`needs: e2e`, `if: always()`) — publishes the E2E videos to GitHub Pages so reviewers can **watch recordings inline**, and **upserts** a PR comment (marker `<!-- e2e-video-report -->`) linking `runs/{run_id}/`. Runs `.github/scripts/generate-pages.sh`, which writes per-run pages + a newest-first root table and prunes runs >90 days / beyond the newest 50 (generator covered by `tests/scripts/generate-pages.test.sh`); uses only `GITHUB_TOKEN`. **One-time setup:** a repo admin must enable Pages (Settings → Pages → branch `gh-pages` / `/`).

### `.github/workflows/storyboard.yml` (name: **CI - Storyboard**) — runs on PRs to `main`
Runs **in parallel** with the `CI - Tests` workflow. It builds `Farkle.E2eTests` and runs only the storyboard-tagged tests (`--filter "Category=Storyboard"`), which boot an **in-memory backend (no Testcontainers / Docker)** and capture multi-viewport screenshots of the opening flow. Two jobs:
1. **`storyboard`** — installs Playwright Chromium, runs the capture, uploads `storyboard-screenshots-<run_id>` + TRX.
2. **`deploy-screenshots`** — publishes the frames to GitHub Pages via `generate-pages.sh` (`MODE=screenshots`) and **upserts** a PR comment (marker `<!-- e2e-storyboard-report -->`) linking `runs/{run_id}/storyboard.html`.

`generate-pages.sh` is **dual-mode** (`MODE=videos` default | `screenshots`); the e2e and storyboard publishers both write into the same `runs/{id}/` tree and share a `concurrency: gh-pages-publish` group so they don't race on `gh-pages` (its generator logic is covered by `tests/scripts/generate-pages.test.sh`, run manually).

### `.github/workflows/codeql.yml` (name: **CI - CodeQL**)
Runs on push/PR to `main` and weekly (Mon 08:00 UTC). Builds the solution and runs CodeQL C# analysis.

### Diagnosing E2E Failures
The `e2e` job uploads:

| Artifact | Contents |
|----------|----------|
| `e2e-trx-<run_id>` | Full TRX with error messages and stack traces |
| `e2e-logs-<run_id>` | Structured log output captured during the run |
| `e2e-videos-<run_id>` | `.webm` recordings (e.g. `HappyPath.webm`, `HappyPath-Bob.webm`) |
| `e2e-screenshots-<run_id>` | PNGs (e.g. `before-first-keep.png`) |

The workflow also posts failing test names + truncated error messages directly to the PR (in the upserted `deploy-pages` comment, alongside the inline-video link). **Check the PR comment first** before downloading artifacts — and use the GitHub Pages link to watch the recordings in-browser.

---

## Testing Patterns

### Testing Layers — what belongs where

Pick the layer that can answer the question with the least machinery. Overlap is fine **only** when each layer is asserting something the others can't.

| Layer | Project | Owns | Does **not** own | Heuristic |
|---|---|---|---|---|
| **Domain unit** | `Farkle.Tests` | All business rules: validators, scoring, state replay, event emission. Pure aggregate behaviour with `IRandom` mocked. | HTTP, DOM, DI wiring, SignalR. | *"If the test wouldn't change when we swap FastEndpoints for ASP.NET MVC, it belongs here."* |
| **Handler unit (frontend)** | `Farkle.SpaTests/Handlers` | BlazorState `Handler` classes in isolation: mocked `IGameService`, dispatch the action, assert `GameState` mutation (`DiceInPlay`, `TurnScore`, `Scoreboard`, `Error`). No bUnit context, no DOM. | Business rules — the client is a thin shell over the API; trust service responses. Component rendering. | *"Given a state and a mocked service response, what does the store look like after?"* |
| **Component (bUnit)** | `Farkle.SpaTests/Components` | Rendering, conditional UI (`Disabled`, visibility), event wiring (clicking Roll dispatches `RollDice.Action`), CSS-class invariants. Mocked `IGameService` + `IGameHubService`. | State-machine internals — that's the handler's job. End-to-end flows — that's E2E. | *"Given this state, does the DOM look right and do clicks fire the right actions?"* |
| **Web integration** | `Farkle.WebTests` | HTTP contract (status codes, JSON shape), FastEndpoints routing, Eventuous round-trip, SignalR broadcast, Identity/JWT, EF migrations. Real Postgres + EventStore via Testcontainers. | Exhaustive business-rule coverage (already in domain unit) — keep to one happy + one representative error path per endpoint. Frontend rendering. | *"Does the wire format and the wiring still hold together?"* |
| **E2E** | `Farkle.E2eTests` | Real-browser flow: WASM hydration, two-player happy path, SignalR turn flip, CSS layout, win condition. Playwright + real backend + real DB. | Edge-case business rules; every error path. Anything that would be faster and just as informative as a unit test. | *"Can a real user, in a real browser, complete a meaningful journey?"* |

**Anti-patterns to avoid:**
- Re-asserting "can't roll out of turn" in `Farkle.WebTests` — already covered by `RollShould.cs`. Integration should prove the rejection becomes an HTTP 400 with the right error payload, not re-prove the rule.
- Driving the BlazorState `Sender` inside a bUnit component test to set up state. If the test is about handler behaviour, write it under `Handlers/` without bUnit. If it's about DOM, set state through the store and render the component.
- Adding a UI-side copy of a domain rule (e.g. "Pass disabled until rolled") just to test it. The validator is the source of truth; the client surfaces the resulting error.

### Layer-specific setup

#### Unit Tests (`Farkle.Tests`)
Base class `GameWithThreePlayersTest` mocks `IRandom` and pre-loads a three-player game:
```csharp
var game = new Game(_randomProvider);
game.Start(new StartGame(1));
game.JoinPlayer(new JoinPlayer(1, "David"));
game.JoinPlayer(new JoinPlayer(1, "Cristian"));
game.JoinPlayer(new JoinPlayer(1, "German"));
```
Tests assert on `game.State` and `game.Changes` (emitted events). Domain tests live under `tests/Farkle.Tests/Domain/`.

#### Integration Tests (`Farkle.WebTests`)
`WebApplicationFactory<Program>` + Testcontainers (PostgreSQL, EventStore): full DI pipeline, real HTTP client, containers per fixture, auto-applied migrations. `GameHubShould` covers the SignalR hub.

#### E2E Tests (`Farkle.E2eTests`)
Playwright (`Microsoft.Playwright` 1.50.0) drives two browser contexts (Alice + Bob) through the happy path until a win. `PlaywrightFixture.NewContextWithVideoAsync` records a `.webm` per session; `InMemoryLoggerProvider` captures structured logs into the `e2e-logs` artifact. Waits for WASM hydration (≈30s) before interacting.

#### Storyboard screenshots (`Farkle.E2eTests`, `Category=Storyboard`)
Multi-viewport screenshots of the opening flow live **in the E2E project** so they reuse the player-advancing helpers (`GameFlow`), but are tagged `[Trait("Category","Storyboard")]` and use a separate, lazy collection fixture (`StoryboardFixture` → in-memory `IAggregateStore`). xUnit instantiates a fixture only when a selected test needs it, so `--filter "Category=Storyboard"` runs **without** booting Testcontainers. Frames land in `test-results/storyboard/{step}-{viewport}.png` (steps `01-landing … 06-pass`; viewports mobile/medium/large).

**This is the loop for iterating on UI changes locally:**
```bash
dotnet build tests/Farkle.E2eTests/Farkle.E2eTests.csproj
PLAYWRIGHT_CHROMIUM_EXECUTABLE_PATH=/usr/bin/microsoft-edge \
  dotnet test tests/Farkle.E2eTests/Farkle.E2eTests.csproj --no-build \
  --filter "Category=Storyboard"
```
- **Chromium in restricted sandboxes:** the Playwright CDN is blocked. Install Edge from `packages.microsoft.com` and point the fixture at it via `PLAYWRIGHT_CHROMIUM_EXECUTABLE_PATH` (the fixture honours that env var). `pwsh` (for `playwright.ps1`) is also installable from there.
- **No-scroll check:** the capture is full-page, so a frame's PNG height *equal to* the viewport height (mobile 844 / medium 800 / large 1080) means it fits; greater means it scrolls.
- **Seeing multi-pip dice:** `ScriptedRandom` returns the minimum (six `1`s) for determinism. To eyeball faces 2–6, *temporarily* vary it, capture, then revert.

#### SPA Tests (`Farkle.SpaTests`)
Two sub-layers in one project, separated by folder:

- **`Components/`** — bUnit (`bunit` 2.7.2) component tests. Inherit `GameBunitContext` (registers MudBlazor, BlazorState, mocked `IGameService`/`IGameHubService`). Assert on rendered DOM and on what user interactions dispatch.
- **`Handlers/`** — Plain xUnit tests against `Handler` classes. Use `HandlerTestContext` (minimal `IStore` + mocked `IGameService`). No bUnit, no DOM. Assert state-after-action.
- `Services/` covers the `IGameService` adapter over the Kiota client (mocked HTTP).
- `Architecture/` holds `ComponentArchitectureShould.cs` and similar invariants.

---

## Important Implementation Notes

### Event Type Registration
`SetUpFarkleModule()` calls `TypeMap.RegisterKnownEventTypes()` to register the versioned event types with Eventuous for serialization. This runs at startup outside the `NSwag` environment.

### FastEndpoints Discovery
Auto-discovery is **disabled**; assemblies are listed explicitly in `Program.cs`:
```csharp
.AddFastEndpoints(o =>
{
  o.Assemblies = new[]
  {
    typeof(Farkle.Endpoints.StartGame).Assembly,
    typeof(RegisterEndpoint).Assembly
  };
  o.DisableAutoDiscovery = true;
});
```
When adding endpoints, ensure their assembly is in this list.

### Module Composition
The `Farkle` module is registered as a self-contained extension in `Program.cs`:
```csharp
services.AddFarkleModuleServices(builder.Configuration, logger, new List<Assembly>());
app.SetUpFarkleModule();   // skipped in the NSwag environment
```
New infrastructure (stores, clients) should be added in `FarkleModuleServiceExtensions`, not directly in `Program.cs`.

### NSwag Environment
NSwag runs the host with `noBuild=true` to extract the OpenAPI spec. In that environment the static-asset manifest and the Farkle module setup are skipped (`!app.Environment.IsEnvironment("NSwag")` guards). Don't break those guards.

### Pinned Versions (upgrades tracked separately)
- **Eventuous** `0.15.0-beta` (stable 0.16.x changes the command-handler API)
- **FastEndpoints** `5.x` (8.x requires API updates)
- **Kiota** `1.31.1` (pinned in `.config/dotnet-tools.json`)

---

## Generated Files Policy

**Never hand-edit these files** — they are auto-generated and any manual changes will be overwritten:

- `src/WebApp.Client/swagger.json` — generated by NSwag from the ASP.NET app
- `src/Farkle.ApiClient/**` — generated by Kiota from `swagger.json` (single shared client used by both `WebApp.Client` and `Farkle.WebTests`)

After any API-contract change (edit the DTO in `src/Farkle.Contracts/`), **regenerate `swagger.json` + the Kiota client and commit the result** — CI's `verify-generated` job fails if the committed files differ. Step-by-step commands: [`docs/api-client-generation.md`](docs/api-client-generation.md).

---

## Common Workflows

### Adding a New Game Command
1. Define the command record in `Domain/GameAggregate/Command.cs`.
2. Add a handler method to `Game` (e.g. `public void RollDice(Command.RollDice cmd)`), applying the appropriate (V2) event.
3. Register it in `GameService`: `.On<Command.X>().InState(Existing).Execute((game, cmd) => game.X(cmd))`.
4. Create an endpoint in `Endpoints/` extending `TypedEndpoint<Request, Response>`.
5. Add request/response DTOs to `Contracts/HttpRequests.cs` / `HttpResponses.cs`.
6. Ensure the endpoint assembly is listed in `Program.cs` FastEndpoints config.
7. Regenerate `swagger.json` + the Kiota client (see Generated Files Policy).

### Adding Validation to a Command
1. Create a `Validator` subclass in `GameValidator.cs`.
2. Add a case to `GameValidator.ValidatePreconditions()`.
3. Compose with `.And()` where multiple validators apply.
4. On failure, return an event implementing `IErrorEvent` so the application layer surfaces it as an HTTP error.

### Extending Game State
1. Add a property to the `GameState` record with `private init`.
2. Add a static handler: `private static GameState HandleX(GameState state, XEvent e) => state with { ... }`.
3. Register it in the `GameState` constructor: `On<XEvent>(HandleX)`.

### Adding/Changing Real-Time Updates
1. Add a method to `IGameEventBroadcaster` and implement it in `SignalRGameEventBroadcaster` (server) using `IHubContext<GameHub>` and group `game-{gameId}`.
2. Add the matching `.On<T>("MessageName", ...)` listener + event in `GameHubService` (client).
3. Dispatch a BlazorState action from the component subscribing to the client event.

---

## Troubleshooting

### E2E Tests Time Out on WASM Hydration
- Ensure dependencies are up: `docker-compose ps`
- Check the browser console / `e2e-logs` artifact for JS errors
- Slow machines may need a longer hydration wait in the test

### EventStore Connection Failed
Verify `ConnectionStrings:Esdb` in `appsettings.Development.json`:
```json
"Esdb": "esdb://admin:changeit@localhost:2113?tls=false"
```
EventStore must be running: `docker-compose up esdb`.

### Database Migration Issues
```bash
dotnet ef database drop -p src/WebApp/WebApp.csproj -f
dotnet ef database update -p src/WebApp/WebApp.csproj
```
The app re-seeds `player1@email.com` on startup if missing.

---

## Contributing & PR Standards

- **Warnings as errors**: all compiler warnings must be resolved.
- **Event versioning**: never modify V1 events; create a V2 with the new schema.
- **Generated files**: regenerate and commit `swagger.json` + `Farkle.ApiClient/` after any contract change (CI enforces this).
- **E2E test videos**: ensure videos/screenshots are uploaded and linked on the PR.
- **Architecture/domain isolation**: keep the domain layer free of infrastructure dependencies.
- **Test coverage**: new domain logic requires unit tests; endpoints require integration tests; every new feature requires at least one E2E test covering the happy path.
- **UI changes**: verify with the storyboard capture and keep the no-scroll constraint at all three viewports (mobile/medium/large); style via component-scoped CSS (`::deep` for MudBlazor children), not inline styles, and don't rename button labels the tests select by text.
- **Close issues via PR**: every PR that resolves an issue MUST include `Closes #<issue>` (or `Fixes #<issue>`) in the body. A bare `#N` is not enough — the keyword is required.
- **TDD commit convention** (Red–Green):
  - **Commit 1 (Red):** failing tests only — must fail before the fix.
  - **Commit 2+ (Green):** implementation that makes them pass. Never mix test and fix changes in one commit.

---

## Agent Workflow for Features

### Starting a New Feature
Always sync with the latest `main` first, then branch from it:
```bash
git checkout main
git pull origin main
```
Never start new work from a stale or unrelated branch.

### PR & CI Loop
1. **Write E2E tests** for the happy path before/alongside the implementation.
2. **Open a PR** targeting `main` once the feature and tests are committed.
3. **Subscribe to PR activity** with `subscribe_pr_activity` immediately after opening so CI results and review comments arrive automatically — do not poll.
4. **Wait for CI**. The relevant signals are the `test` job and the `e2e` job:
   - Pass → wait for the PR to be **merged** before starting the next feature.
   - Fail → diagnose from the PR comment / artifacts and push a fix.

### 5-Commit Limit
If E2E tests are still failing after **5 commits** on the PR branch:
1. **Close the PR** without merging.
2. **Delete the branch**.
3. **Open a new issue** (or comment on the originating issue) summarizing: the intended implementation, each approach tried and why it failed (with error messages from the TRX/logs artifacts), and the test(s) still failing with the last observed failure reason.
4. **Stop work** — leave the summary for the next session to start fresh with full context.
