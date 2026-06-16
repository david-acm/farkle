# Farkle vs. Ardalis Clean Architecture

A descriptive, side-by-side comparison of Farkle's architecture with the canonical
[Ardalis Clean Architecture](https://github.com/ardalis/CleanArchitecture) template — across
**principles** and **code organization**.

> **Scope & stance.** This is an *analysis*, not a proposal. Farkle is an Event-Sourcing / CQRS
> sample built on **Eventuous + EventStoreDB**, a deliberate departure from Ardalis's default
> **EF Core + MediatR** stack. Divergences are therefore described as intentional trade-offs;
> only a handful are flagged as genuine architectural gaps that hold *regardless* of the
> Event-Sourcing choice. Nothing here recommends a refactor.

---

## 1. TL;DR verdict

Farkle honours the **spirit** of Clean Architecture — dependencies point inward, the domain is
rich and encapsulated, infrastructure is inverted behind interfaces, and the test pyramid is
strong — but it **packages the layers differently**: Domain, Application (use cases) and the HTTP
layer all live in a single `Farkle` project, and there is no dedicated Infrastructure project.
Ardalis splits these into separate projects so the "core" can carry *zero* framework/infrastructure
dependencies; Farkle's `Farkle` project, by contrast, references the event-store client and the web
framework directly.

| Principle | Verdict |
|---|---|
| Dependency Rule (point inward) | ✅ Aligned (project graph is acyclic, inward) |
| Domain-centric, rich model | ✅ Aligned (aggregate enforces invariants; no anemic models) |
| Dependency inversion for infrastructure | 🟡 Partial (read-model/broadcast inverted; event store + web framework referenced directly by `Farkle`) |
| "Infrastructure/DB is a detail (plugin)" | 🟡 Partial (Postgres/SignalR are plugins; ESDB/Eventuous and FastEndpoints are compiled into the core project) |
| Encapsulation / testability | ✅ Aligned (internal domain, `IRandom` seam, ArchUnit guards, 4-layer test suite) |
| Project-per-layer organization | 🔵 Divergent by design (one module project instead of Core/UseCases/Infrastructure/Web) |

Legend: ✅ aligned · 🟡 partial · 🔵 divergent-by-design.

---

## 2. Principles comparison

| Principle | Ardalis intent | Farkle reality | Verdict |
|---|---|---|---|
| **Dependency Rule** — source dependencies point only inward, toward the domain. | Web → UseCases/Infrastructure → Core; Core depends on nothing. | Acyclic graph pointing inward: `WebApp → Farkle → {Farkle.Contracts, Farkle.SharedKernel}` (see §4). `tests/Farkle.Tests/DomainClassesShould.cs` even asserts (via ArchUnitNET) that `Farkle.Domain.*` types don't depend on infrastructure and stay `internal`. | ✅ |
| **Domain-centric design** — architecture organized around the model, not the database/framework. | Entities/aggregates/value objects/domain events in Core. | `src/Farkle/Domain/GameAggregate/` holds the `Game` aggregate, `GameState`, versioned `GameEvents`, value objects (`Score`, `GameId`, `DieValue`, `Player`, `Dice`) and `GameValidator`. The model — not the store — is the center. | ✅ |
| **Dependency inversion for infrastructure** — outer layers implement interfaces defined inward. | Repositories/services implement Core interfaces. | Done for the read/notify side: `IGameViewStore` and `IGameEventBroadcaster` (in `src/Farkle/Application/`) are implemented by `WebApp` (`ReadModel/EfGameViewStore.cs`, `Hubs/SignalRGameEventBroadcaster.cs`), with no-op defaults (`NullGameViewStore`) so the module runs without Postgres. **But** the event store itself is not inverted: `Farkle.csproj` references `EventStore.Client.Grpc` + `Eventuous.EventStore` directly (the store is abstracted by Eventuous' `IAggregateStore`, configured in `FarkleModuleServiceExtensions.cs`, rather than by a Farkle-owned interface). | 🟡 |
| **Infrastructure/DB is a detail (a plugin)** — DB/UI/framework are replaceable. | EF Core, external services confined to Infrastructure. | Postgres (Identity + read model), SignalR and Identity are confined to the `WebApp` host and swappable (the storyboard test factory swaps the store for in-memory). However ESDB/Eventuous **and the web framework (FastEndpoints)** are package references of the `Farkle` project, so they are compiled into the "core" rather than plugged in around it. | 🟡 |
| **Encapsulation & rich model** — invariants enforced inside the model; no anemic entities. | Aggregates form consistency boundaries; value objects immutable. | The `Game` aggregate enforces all rules through `GameValidator` before applying events; invalid commands yield **error events** instead of mutating state. Value objects are immutable records/SmartEnums. Domain types are `internal` (guarded by ArchUnit). | ✅ |
| **Testability** — business logic testable without DB/UI/framework. | Unit/Integration/Functional layering. | Domain unit tests mock `IRandom` and exercise the aggregate with no I/O; integration tests use Testcontainers; component tests use bUnit; E2E uses Playwright. See §7. | ✅ |

---

## 3. Stack at a glance

| Concern | Ardalis default | Farkle |
|---|---|---|
| Persistence | EF Core (state-based) + repositories | EventStoreDB (event-sourced) via Eventuous; EF Core/Postgres only for Identity + read-model snapshots |
| Write model | Entities saved through `IRepository<T>` | `Game : Aggregate<GameState>`; events appended to stream `Game-{id}` |
| Command dispatch | MediatR command handlers | Eventuous `CommandService<Game, GameState, GameId>` (fluent `.On<>().InState().Execute()`) |
| Query/read side | Repository + `Ardalis.Specification` | Incremental projection (`GameViewProjector`) → `IGameViewStore` snapshot; replay fallback |
| HTTP layer | `Ardalis.ApiEndpoints` / FastEndpoints / Minimal API | FastEndpoints (`TypedEndpoint<TRequest,TResponse>`) |
| Validation | FluentValidation + `Ardalis.GuardClauses` | Custom `Validator`/`ValidationResult` composite + **validation-as-events** |
| Result pattern | `Ardalis.Result` | `Ardalis.Result(.AspNetCore)` wrapping Eventuous `Result` (`Farkle/Application/ResultExtensions.cs`) |
| Domain base types | `Ardalis.SharedKernel` (`EntityBase`, `IAggregateRoot`, `DomainEventBase`) | Eventuous `Aggregate<TState>` / `State<TState>`; no Ardalis base types |
| Shared kernel | `Ardalis.SharedKernel` package | Hand-rolled `Farkle.SharedKernel` project = infra-free `Scoring/ScoreCalculator`, shared by server domain **and** the Blazor client |
| Client | (none in template) | Blazor WASM (`WebApp.Client`) + BlazorState; Kiota client (`Farkle.ApiClient`) |

---

## 4. Code organization

### 4.1 Ardalis canonical layout

```
src/
  *.Core            (domain: entities, aggregates, value objects, domain events,
                     specifications, interfaces; base types from Ardalis.SharedKernel) — depends on nothing
  *.UseCases        (application: MediatR command/query handlers, validators)        — depends on Core
  *.Infrastructure  (EF Core DbContext, repositories, external services)             — depends on Core (+ UseCases)
  *.Web             (FastEndpoints/Minimal API endpoints; composition root)          — depends on UseCases + Infrastructure
tests/
  *.UnitTests  *.IntegrationTests  *.FunctionalTests
```
Dependency direction: `Web → {UseCases, Infrastructure} → Core`. Core is framework-free; Infrastructure
implements the interfaces Core/UseCases declare.

### 4.2 Farkle actual layout

Project dependency graph (from each `.csproj`'s `ProjectReference` — acyclic, inward):

```
Farkle              -> Farkle.Contracts, Farkle.SharedKernel
Farkle.Contracts    -> (none)
Farkle.SharedKernel -> (none)
Farkle.ApiClient    -> (none, Kiota-generated)
WebApp.Client       -> Farkle.ApiClient, Farkle.Contracts, Farkle.SharedKernel
WebApp              -> Farkle, WebApp.Client
tests:  Farkle.Tests -> Farkle
        Farkle.WebTests -> Farkle, WebApp, Farkle.ApiClient
        Farkle.SpaTests -> Farkle, WebApp.Client
        Farkle.E2eTests -> WebApp
```

The single `Farkle` project folds three Clean-Architecture layers into one assembly (separated by
folder/namespace and `internal` visibility rather than project boundary):

```
src/Farkle/
  Domain/GameAggregate/   Game, GameState, GameEvents (V1/V2), Command,
                          GameValidator, DefaultRandomProvider, EventExtensions
  Domain/                 Validator, ValidationResult (composite-validator base)
  Application/            GameService (CommandService), GameCreator, GameIdGenerator,
                          GameViewProjector, GameBroadcastHandler, GameStateSerializer,
                          ResultExtensions, CommandHandlerBuilderExtensions,
                          IGameViewStore, IGameEventBroadcaster
  Endpoints/              StartGame/JoinPlayer/BeginGame/RollDice/KeepDice/PassTurn/
                          GetGameState (FastEndpoints) + TypedEndpoint base + *Mapper.cs
  FarkleModuleServiceExtensions.cs   (DI + Eventuous/ESDB wiring)
  Assembly.cs                        ([InternalsVisibleTo] for test projects)

src/Farkle.SharedKernel/   Scoring/ScoreCalculator (pure, infra-free; shared by domain + client)
src/Farkle.Contracts/      HttpRequests / HttpResponses (dependency-free DTOs)
```

Note that the two web helpers a reader might expect in a "shared kernel" actually live **inside**
the `Farkle` project: `TypedEndpoint` (the FastEndpoints base) in `Endpoints/`, and `ResultExtensions`
(Eventuous-`Result`→HTTP mapping) in `Application/`. `Farkle.SharedKernel` instead holds *domain*
logic (`ScoreCalculator`), which both the server aggregate and the WASM client reference — closer to
the DDD meaning of a shared kernel.

Infrastructure is **not** a single project — it is split:
- inside `Farkle` (event-store wiring in `FarkleModuleServiceExtensions.cs`, plus the ESDB/Eventuous
  package references in `Farkle.csproj`), and
- inside the host `WebApp/` (`Auth/` Identity+EF/Postgres, `ReadModel/` `EfGameViewStore` +
  `ReadModelDbContext` + `PostgresCheckpointStore`, `Hubs/` SignalR, `Program.cs` composition root).

Note the read model is itself split by concern: the **projection logic** lives in the core
(`src/Farkle/Application/GameViewProjector.cs`) while its **EF persistence** lives in
`src/WebApp/ReadModel/` behind `IGameViewStore` — a clean inversion.

### 4.3 Layer mapping

| Ardalis project | Where it lives in Farkle |
|---|---|
| `*.Core` (domain) | `src/Farkle/Domain/**` |
| `*.UseCases` (application) | `src/Farkle/Application/**` |
| `*.Web` (endpoints + composition root) | `src/Farkle/Endpoints/**` (endpoints) + `src/WebApp/Program.cs` (composition root) |
| `*.Infrastructure` | **No dedicated project** — split between `Farkle` (ESDB/Eventuous wiring) and `WebApp` (EF/Postgres, SignalR, Identity) |
| `Ardalis.SharedKernel` (DDD base types) | *Package not used* (Eventuous supplies aggregate/state base types). Farkle has its **own** `Farkle.SharedKernel` project, but for **shared infra-free domain logic** — `Scoring/ScoreCalculator`, the single source of truth reused by the server domain *and* the Blazor client — which matches the *purpose* of a DDD shared kernel (see §5). |
| Contracts/DTOs (usually inside Web/UseCases) | `src/Farkle.Contracts/**` (extracted as its own dependency-free project) |
| *(no analog)* | `WebApp.Client` (Blazor WASM), `Farkle.ApiClient` (Kiota client) |

---

## 5. Building blocks

| Ardalis building block | Farkle |
|---|---|
| `Ardalis.Result` / `.AspNetCore` | ✅ Used (`Farkle.csproj`), wrapping Eventuous `Result` → HTTP in `Farkle/Application/ResultExtensions.cs` (409/404/400 mapping). |
| `Ardalis.SmartEnum` | ✅ Used for `DieValue` (`Domain/GameAggregate/GameEvents.cs`). |
| `Ardalis.GuardClauses` | 🟡 Referenced as a package; the domain favours the composite `Validator` + validation-as-events over guard clauses. |
| `Ardalis.SharedKernel` base types (`EntityBase`, `IAggregateRoot`, `DomainEventBase`, `IDomainEventDispatcher`) | ❌ Package not used — Eventuous `Aggregate<TState>` / `State<TState>` + `[EventType]` events. Farkle's own `Farkle.SharedKernel` project instead carries shared *domain* logic (`Scoring/ScoreCalculator`) reused by server + client. |
| `Ardalis.Specification` | ❌ N/A — there is no `IQueryable`/repository query surface to encapsulate under Event Sourcing. |
| `Ardalis.ApiEndpoints` | ❌ Uses **FastEndpoints** instead (which recent Ardalis templates also adopt) via `TypedEndpoint<,>` (`Farkle/Endpoints/TypedEndpoint.cs`). |
| MediatR (CQRS + domain-event dispatch) | ❌ Server uses Eventuous `CommandService`; the **client** uses BlazorState (a MediatR-like Redux loop). |
| FluentValidation | ❌ Custom composite `Validator`/`ValidationResult`; invalid commands emit `IErrorEvent`s surfaced as HTTP 400. |

---

## 6. CQRS & persistence

**Ardalis:** command/query separation is *optional* and typically realized with MediatR handlers over
EF Core; reads use repositories + specifications, often against the same database as writes.

**Farkle:** CQRS is intrinsic to the Event-Sourcing design:
- **Write side** — `GameService` (`CommandService<Game, GameState, GameId>`) loads the aggregate,
  validates, and appends events to ESDB (stream `Game-{id}`), with optimistic concurrency.
- **Read side** — `GameViewProjector` subscribes to the event stream and folds **one event at a
  time** onto the stored snapshot (`state.When(...)`), persisting it via `IGameViewStore`
  (`EfGameViewStore`/Postgres). `GetGameStateEndpoint` serves this snapshot and **falls back to
  aggregate replay** when the view is absent (e.g. hosts without Postgres).
- **Real-time** — `GameBroadcastHandler` reacts to committed events and pushes updates over SignalR
  (`IGameEventBroadcaster` → `SignalRGameEventBroadcaster`).

So Farkle has *two* read paths (materialized snapshot + replay fallback) and a push channel, where the
Ardalis default has a single repository read path. Scoring rules are not duplicated across this split:
the pure `ScoreCalculator` (`Farkle.SharedKernel`) is the single source of truth, used by the aggregate
on the write side and by the Blazor client for a live turn-score preview.

---

## 7. Testing layering

| Ardalis layer | Farkle equivalent |
|---|---|
| Unit (Core.Tests) | `tests/Farkle.Tests` — aggregate/validator/scoring/state with `IRandom` mocked; plus ArchUnitNET rules in `DomainClassesShould.cs` (domain stays internal, doesn't reference infrastructure). |
| Integration (UseCases/Infrastructure.Tests) | `tests/Farkle.WebTests` — `WebApplicationFactory` + Testcontainers (Postgres + EventStore); HTTP contract + SignalR + Identity round-trips. |
| Functional / E2E (Web/FunctionalTests) | `tests/Farkle.E2eTests` — Playwright two-player happy path (+ a Storyboard capture using an in-memory store, no Docker). |
| *(no analog)* | `tests/Farkle.SpaTests` — bUnit component tests + BlazorState handler tests for the Blazor client. |

Farkle's pyramid is a **superset**: it adds dedicated component (bUnit) and browser (Playwright)
layers on top of the unit/integration/functional trio because it ships a real SPA.

---

## 8. Divergences: intentional vs. genuine gaps

**Intentional (by design — driven by the Event-Sourcing/CQRS teaching goal):**
- Event Sourcing (ESDB) instead of EF-Core state; events are the source of truth.
- Eventuous `CommandService` instead of MediatR; BlazorState on the client.
- Validation-as-events instead of throwing/FluentValidation.
- Eventuous base types instead of `Ardalis.SharedKernel`; no `Ardalis.Specification` (no query surface).
- A single `Farkle` *module* project for a small, single-bounded-context game, separating layers by
  folder + `internal` visibility rather than by project.
- Scoring extracted into an infra-free `Farkle.SharedKernel` (`ScoreCalculator`) reused by both the
  server domain and the Blazor client — a Clean-Architecture-friendly shared kernel (one source of
  truth, no duplicated rules between back end and UI).

**Genuine gaps (would hold even if the stack were EF + MediatR):**
- The de-facto "core" (`Farkle`) compiles against **infrastructure** (`EventStore.Client.Grpc`,
  `Eventuous.EventStore`) and the **web framework** (`FastEndpoints`), so it is not framework-free the
  way Ardalis's Core is.
- **HTTP endpoints share the project with the domain** (`Endpoints/` next to `Domain/`), mixing a Web
  concern into the core module.
- **No dedicated Infrastructure project** — infrastructure is split between `Farkle`'s ESDB wiring and
  the `WebApp` host, so there is no single "infrastructure is a plugin" boundary.
- The ArchUnit rule `DomainTypesShouldNotReferenceInfrastructure` encodes the *intent* of the
  dependency rule, but as written its "infrastructure" set is largely empty, so it is closer to
  aspirational than enforcing.

These observations are recorded neutrally; given Farkle's purpose as an ES/CQRS sample, none are
defects to "fix" — they simply mark where the packaging differs from the Ardalis canon.

---

## Appendix — sources for the Ardalis canon

- Ardalis Clean Architecture template — https://github.com/ardalis/CleanArchitecture
- "Clean Architecture with ASP.NET Core" — https://ardalis.com/clean-architecture-asp-net-core/
- `Ardalis.SharedKernel` — https://www.nuget.org/packages/Ardalis.SharedKernel
- `Ardalis.Result` — https://www.nuget.org/packages/Ardalis.Result
- `Ardalis.Specification` — https://www.nuget.org/packages/Ardalis.Specification
- eShopOnWeb reference app — https://github.com/MicrosoftLearning/eShopOnWeb
- .NET microservices architecture guide — https://learn.microsoft.com/dotnet/architecture/microservices/
