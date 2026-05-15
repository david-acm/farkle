# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Repository Overview

**Farkle** is a sample .NET application demonstrating Event Sourcing, CQRS, Clean Architecture, and Event Modelling patterns. It implements the Greedy dice game as a backend API (WebApi) and frontend (Blazor Server + WASM).

The codebase prioritizes architectural patterns and test-driven development:
- **Event Sourcing** with Eventuous framework and EventStore (ESDB)
- **CQRS** separation of commands and queries
- **Domain-Driven Design** with aggregate roots and validation
- **Comprehensive testing** (unit, integration, E2E with Playwright)

### Key Technologies

| Layer | Stack |
|-------|-------|
| Backend API | .NET 10, FastEndpoints, Eventuous |
| Database | PostgreSQL (Identity), EventStore (Event Store) |
| Frontend | Blazor Server + Blazor WASM, MudBlazor, BlazorState |
| Testing | xUnit, Playwright, Testcontainers, FluentAssertions |
| Infrastructure | Docker Compose, GitHub Actions CI/CD |

---

## Project Structure

```
src/
├── Farkle/                    # Core domain module (Event Sourcing)
│   ├── Domain/GameAggregate/  # Aggregate root, events, validation (~800 lines)
│   ├── Application/           # GameService (command service)
│   ├── Endpoints/             # FastEndpoints (StartGame, RollDice, KeepDice, PassTurn, JoinPlayer)
│   └── FarkleModuleServiceExtensions.cs  # DI registration
├── Farkle.Contracts/          # HTTP request/response DTOs (no dependencies)
├── Farkle.SharedKernel/       # Shared utilities (Result extensions, validators)
├── WebApp/                    # Blazor Server host + Identity/Auth (PostgreSQL)
├── WebApp.Client/             # Blazor WASM client, state management (BlazorState)
└── Scripts/                   # Utility scripts

tests/
├── Farkle.Tests/              # Unit tests for domain (Game, validators)
├── Farkle.WebTests/           # Integration tests (API via WebApplicationFactory, Testcontainers)
├── Farkle.E2eTests/           # End-to-end tests (Playwright, videos uploaded to PR)
└── Farkle.SpaTests/           # Component tests (bUnit) for WASM components
```

---

## Architecture: Event Sourcing & CQRS

### Event Sourcing Pipeline

All game state mutations are captured as immutable events persisted to EventStore:

1. **Commands** (`Command.*`) → HTTP requests validated at endpoint
2. **Aggregate** (`Game`) → Applies command, emits event(s) to validate
3. **Validators** (`GameValidator.*`) → Pre-conditions checked before event is applied
4. **Events** (`GameEvents.V1.*`, `V2.*`) → Immutable facts stored in ESDB
5. **State** (`GameState`) → Rebuilt by replaying events via `On<EventType>()` handlers

**Key files:**
- `/src/Farkle/Domain/GameAggregate/Game.cs` - Aggregate root with command handlers
- `/src/Farkle/Domain/GameAggregate/GameState.cs` - State reconstruction from events
- `/src/Farkle/Domain/GameAggregate/GameValidator.cs` - Pre-condition validators (272 lines)
- `/src/Farkle/Domain/GameAggregate/GameEvents.cs` - Event records (V1 & V2 versioned)

### Event Versioning

Two event versions coexist:
- **V1** events: Original `DiceRolled`, `DiceKept` without game stage tracking
- **V2** events: Include `GameStage` field for clearer state machine semantics

Handlers in `GameState` support both; the domain uses V2 going forward.

### Command Service & Validation

`GameService` (extends `CommandService<Game, GameState, GameId>`) orchestrates:
- Command routing via fluent builder: `.On<Command.StartGame>().InState(New).Execute(...)`
- Aggregate store persistence via `Eventuous`
- Error event detection and conversion to HTTP results

**Validators** use the **Composite Pattern**:
```csharp
new PlayerIsInTurn(state, e.PlayerId)
  .And(new SingleRoll(state, e.PlayerId))
  .IsSatisfied()
```

---

## Development Commands

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
dotnet test tests/Farkle.Tests/Farkle.Tests.csproj --filter "TestClassName"
```

**Test categories:**
- **Unit tests**: `dotnet test tests/Farkle.Tests/Farkle.Tests.csproj`
- **Integration tests** (WebApi): `dotnet test tests/Farkle.WebTests/Farkle.WebTests.csproj`
- **Component tests** (bUnit WASM): `dotnet test tests/Farkle.SpaTests/Farkle.SpaTests.csproj`
- **E2E tests** (Playwright):
  ```bash
  dotnet test tests/Farkle.E2eTests/Farkle.E2eTests.csproj --filter "FullyQualifiedName~GameHappyPath"
  ```

**E2E test video recordings** are saved to `test-results/videos/{testName}.webm` and uploaded as GitHub artifacts.

### Run the Application Locally

**Prerequisites:** Docker & Docker Compose installed

```bash
# Start dependencies (PostgreSQL, EventStore)
docker-compose up -d

# Run WebApp (Blazor Server + WASM)
dotnet run --project src/WebApp/WebApp.csproj

# Navigate to https://localhost:5001 (or http://localhost:5000)
```

**Default seeded user for testing:**
- Email: `player1@email.com`
- Password: `Pass@word1`

**Configuration** (appsettings.Development.json):
- EventStore: `esdb://admin:changeit@localhost:2113?tls=false`
- PostgreSQL (Identity): `farkle_identity` database on localhost:5432
- Backend URL for client: `http://localhost:5157`

### Linting & Code Quality

The repository uses:
- **.editorconfig** - Enforced formatting rules
- **TreatWarningsAsErrors = true** (Directory.Build.props) - All warnings must be fixed
- **Nullable reference types enabled** - `#nullable enable`
- **Architecture tests** (ArchUnitNET): Validates domain layer isolation

No explicit linter (StyleCop/Roslyn analyzer) beyond compiler settings.

### Database Migrations

The WebApp uses Entity Framework Core with PostgreSQL for Identity:

```bash
# Create a new migration
dotnet ef migrations add <MigrationName> -p src/WebApp/WebApp.csproj

# Apply migrations (automatic on app startup in non-NSwag environment)
dotnet ef database update -p src/WebApp/WebApp.csproj
```

---

## Key Architectural Patterns

### 1. Clean Layered Architecture

- **Domain Layer** (`Farkle`): Game aggregate, events, validators — zero infrastructure dependencies
- **Application Layer** (`Farkle/Application`): GameService orchestrates commands
- **Interface Layer** (Endpoints in `Farkle`): FastEndpoints route HTTP to commands
- **Shared Kernel** (`Farkle.SharedKernel`): Cross-cutting utilities (Ardalis.Result)

### 2. Aggregate Root: Game

The `Game` aggregate encapsulates all game logic:
- **State**: Immutable record `GameState` rebuilt by event replay
- **Commands**: `StartGame`, `JoinPlayer`, `RollDice`, `KeepDice`, `PassTurn`
- **Events**: Versioned (V1, V2) with pre-condition validation
- **Invariants**: Enforced by validators (only current player can roll, can't roll twice, etc.)

**Game flow:**
1. `StartGame` → GameStage = Rolling
2. `JoinPlayer` (multiple times)
3. `RollDice` → dice appear on table
4. `KeepDice` → move scoring dice to hand, update turn score
5. `PassTurn` → lock in score, rotate to next player. If score ≥ 10,000 → `GameWon`

### 3. Domain Validation via Composite Pattern

Validators are composable:
- `PlayerIsInTurn` - Only the current player can act
- `SingleRoll` - Each turn, can roll only once until dice are kept
- `PlayerCanPass` - Can't pass without rolling
- `DiceAreOnesOrFives` - Only 1s and 5s score
- `DiceAreStraight` - 1-2-3-4-5-6 scores 1500 points

All extend abstract `Validator` class with `.And()` fluent builder.

### 4. Command Service & Event Sourcing (Eventuous)

`GameService` extends `Eventuous.CommandService<>`:
- Registers command handlers: `.On<Command.X>().InState(Existing).Execute((game, cmd) => game.Method(cmd))`
- Manages aggregate loading/saving via `IAggregateStore`
- Converts results to HTTP via `Ardalis.Result` extension methods

Event persistence via `EsdbEventStore` (EventStore gRPC client).

### 5. Frontend State Management (BlazorState)

WASM client uses **Redux-like pattern** via `BlazorState`:
- `GameState` record holds game ID, player ID, dice, turn score, error state
- Actions dispatch state mutations
- Bindings to components via DI-injected `GameState` store
- Kiota-generated HTTP client (`FarkleApiClient`) for API calls

---

## Domain Model Concepts

### Game Stages
```csharp
enum GameStage { None, Rolling, Keeping, Finished }
```
- **Rolling**: Player may roll dice
- **Keeping**: Player choosing which dice to set aside
- **Finished**: Winner determined

### Score Calculation
- **Ones**: 100 points each
- **Fives**: 50 points each
- **Three of a kind**: 100 × face value (e.g., three 4s = 400)
- **Straight** (1-2-3-4-5-6): 1500 points
- **Win threshold**: 10,000 points

### Key Value Types

**DieValue** (SmartEnum): One, Two, Three, Four, Five, Six
**Player** (record): `(int Id, string Name)`
**GameState** (record): Immutable state with event handlers
**Score** (record): Int wrapper with implicit conversions

---

## Testing Patterns

### Unit Tests (Farkle.Tests)

Base class `GameWithThreePlayersTest` mocks `IRandom` and pre-loads a game with three players:
```csharp
var game = new Game(_randomProvider);
game.Start(new StartGame(1));
game.JoinPlayer(new JoinPlayer(1, 1, "David"));
game.JoinPlayer(new JoinPlayer(1, 2, "Cristian"));
game.JoinPlayer(new JoinPlayer(1, 3, "German"));
```

Tests assert on `game.State` and `game.Changes` (emitted events).

### Integration Tests (Farkle.WebTests)

Use `WebApplicationFactory<Program>` + `Testcontainers` for PostgreSQL:
- Full DI pipeline, real HTTP client
- Docker container spun up per test fixture
- Database migrations auto-applied

### E2E Tests (Farkle.E2eTests)

`PlaywrightFixture` records browser interactions to `.webm` video:
- Navigates to `/games/{gameId}`
- Waits for WASM hydration (30s timeout on "Roll" button)
- Clicks, drags, and asserts DOM state
- CI workflow uploads videos to GitHub artifacts and comments on PR

### Component Tests (Farkle.SpaTests)

bUnit tests for Blazor WASM components with mocked `FarkleApiClient` and `IGameService`.

---

## Important Implementation Notes

### Event Type Registration

Before running the app (or outside NSwag environment), call:
```csharp
TypeMap.RegisterKnownEventTypes();  // In SetUpFarkleModule()
```
This registers versioned event types with Eventuous for serialization.

### Endpoint Discovery & FastEndpoints

FastEndpoints is configured with **manual assembly specification** (auto-discovery disabled):
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
Add new endpoints and ensure the assembly is listed.

### Migration to Newer Eventuous

The codebase pins `Eventuous 0.15.0-beta` due to API changes in stable 0.16.x. Upgrading is tracked in a separate issue and requires command handler refactoring.

### FastEndpoints Version

Pinned to **5.x** (migration to 8.x requires API updates, tracked separately).

### Configuration via Dependency Injection

The `Farkle` module is registered as a self-contained extension in `WebApp.Program.cs`:
```csharp
services.AddFarkleModuleServices(builder.Configuration, logger, new List<Assembly>());
app.SetUpFarkleModule();
```

New infrastructure (stores, clients) should be added here, not directly in `WebApp.Program.cs`.

---

## Common Workflows

### Adding a New Game Command

1. Define command record in `Domain/GameAggregate/Command.cs`
2. Add command handler method to `Game` aggregate (e.g., `public void RollDice(Command.RollDice cmd)`)
3. Register in `GameService`: `.On<Command.RollDice>().InState(Existing).Execute((game, cmd) => game.RollDice(cmd))`
4. Create endpoint in `Endpoints/` extending `TypedEndpoint<Request, Response>`
5. Add HTTP request/response DTOs to `Contracts/HttpRequests.cs` and `HttpResponses.cs`
6. Register endpoint assembly in `WebApp.Program.cs`

### Adding Validation to a Command

1. Create a `Validator` subclass in `GameValidator.cs`
2. Add case to `GameValidator.ValidatePreconditions()` switch
3. Use fluent `.And()` to compose multiple validators
4. On failure, emit an error event implementing `IErrorEvent` interface

### Extending Game State

1. Add property to `GameState` record with `private init`
2. Add handler method: `private static GameState Handle<EventType>(GameState state, EventType e)`
3. Register in `GameState()` constructor: `On<EventType>(HandleEventType)`
4. Update event handlers to return new state with `.with { Property = value }`

---

## Troubleshooting

### E2E Tests Timeout on WASM Hydration

The test waits 30 seconds for the "Roll" button. If timeout occurs:
- Ensure PostgreSQL and EventStore are running: `docker-compose ps`
- Check browser console for JS errors
- Increase `WasmTimeoutMs` in test if system is slow

### EventStore Connection Failed

Verify `ConnectionStrings:Esdb` in `appsettings.Development.json`:
```json
"Esdb": "esdb://admin:changeit@localhost:2113?tls=false"
```
EventStore must be running: `docker-compose up esdb`

### Database Migration Issues

If Identity schema is out of sync:
```bash
dotnet ef database drop -p src/WebApp/WebApp.csproj -f
dotnet ef database update -p src/WebApp/WebApp.csproj
```
The app re-seeds `player1@email.com` on startup if missing.

---

## Contributing & PR Standards

- **Warnings as errors**: All compiler warnings must be resolved
- **Event versioning**: Don't modify V1 events; create V2 with new schema
- **E2E test videos**: Ensure videos are uploaded and linked on PR (GitHub Actions workflow)
- **Architecture tests**: Must pass to ensure domain isolation
- **Test coverage**: New domain logic requires unit tests; endpoints require integration tests; every new feature requires at least one E2E test covering the happy path

---

## Agent Workflow for Features

### PR & CI Loop

Every feature must follow this cycle:

1. **Write E2E tests** covering the happy path before or alongside the implementation.
2. **Open a PR** targeting `main` once the feature and tests are committed.
3. **Subscribe to PR activity** using `subscribe_pr_activity` immediately after opening the PR so CI results and review comments arrive automatically — do not poll.
4. **Wait for the `E2E Happy-Path Tests` CI job**. When the result arrives:
   - If it passes, the feature is done.
   - If it fails, diagnose using the logs described below and push a fix.

### Diagnosing E2E Failures

The CI workflow uploads two artifacts per run that contain everything needed to diagnose a failure:

| Artifact | Contents |
|----------|----------|
| `e2e-trx-<run_id>` | Full TRX results file with error messages and stack traces |
| `e2e-logs-<run_id>` | Structured log output captured during the test run |
| `e2e-videos-<run_id>` | `.webm` screen recordings of each test |

The PR comment posted by the workflow also surfaces failing test names and truncated error messages directly in the thread, so check the PR comment first before downloading artifacts.

To make failures diagnosable, every E2E test must:
- Use descriptive `Page.GotoAsync` URLs and `Locator` descriptions so Playwright error messages identify the failing element.
- Emit structured log entries at key steps via the app's `ILogger` (picked up by the `e2e-logs` artifact).
- Record a `.webm` video using `PlaywrightFixture.NewContextWithVideoAsync` — name the video file after the test method so it maps 1-to-1 to the CI artifact.

### 5-Commit Limit

If the E2E tests are still failing after **5 commits** on the PR branch:

1. **Close the PR** without merging.
2. **Delete the branch**.
3. **Open a new issue** (or add a comment to the originating issue) containing:
   - A summary of the intended implementation.
   - Each approach attempted and why it failed (copy error messages from the TRX/logs artifacts).
   - The test(s) that are still failing and the last observed failure reason.
4. **Stop work** — leave the summary for the next agent session to start fresh with full context.

