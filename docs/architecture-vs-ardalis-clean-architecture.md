# Farkle vs. Ardalis Clean Architecture

A descriptive, side-by-side comparison of Farkle's architecture with the canonical
[Ardalis Clean Architecture](https://github.com/ardalis/CleanArchitecture) template — across
**principles** and **code organization**.

> **Scope & stance.** This is an *analysis*, not a proposal. Farkle is an Event-Sourcing / CQRS
> sample built on **Eventuous + EventStoreDB**, a deliberate departure from Ardalis's default
> **EF Core + MediatR** stack. Divergences are therefore described as intentional trade-offs;
> only a handful are flagged as genuine architectural gaps that hold *regardless* of the
> Event-Sourcing choice. Nothing here recommends a refactor.

> **Currency.** This document reflects the repo **after the `Farkle.Infrastructure` extraction
> (PR #197)** and the shared turn vocabulary — `TurnActionPolicy` (#286) and the relocated
> `GameStage` enum (#288). A dedicated **`Farkle.Infrastructure`**
> project now owns the ESDB event store, the EF/Postgres read model, SignalR realtime and Identity
> persistence, so the `Farkle` core no longer compiles against those stacks. The dependency
> direction is enforced by **`tests/Farkle.ArchitectureTests`** (ArchUnitNET). Rows that were
> "🟡 partial" before that extraction are updated below.

---

## 1. TL;DR verdict

Farkle honours the **spirit** of Clean Architecture — dependencies point inward, the domain is
rich and encapsulated, infrastructure is inverted behind interfaces, and the test pyramid is
strong. Since the `Farkle.Infrastructure` extraction it also packages most layers the Ardalis way:
the event store, read model, realtime and Identity now live in a dedicated Infrastructure project,
and the `Farkle` core no longer references the ESDB/EF/SignalR/Identity stacks. The **one remaining
packaging divergence** is that Domain, Application (use cases) and the HTTP **Endpoints** still share
the single `Farkle` project — so the web framework (`FastEndpoints`) is compiled into the core —
whereas Ardalis splits Endpoints into a separate `*.Web` project so the core carries zero web
dependencies.

| Principle | Verdict |
|---|---|
| Dependency Rule (point inward) | ✅ Aligned (acyclic inward graph, enforced by `Farkle.ArchitectureTests`) |
| Domain-centric, rich model | ✅ Aligned (aggregate enforces invariants; no anemic models) |
| Dependency inversion for infrastructure | ✅ Aligned (event store, read model, broadcast, Identity all inverted; core depends on abstractions only) |
| "Infrastructure/DB is a detail (plugin)" | 🟡 Mostly (ESDB/EF/SignalR/Identity are a swappable `Farkle.Infrastructure` plugin; the **web framework** is still compiled into the core because Endpoints live there) |
| Encapsulation / testability | ✅ Aligned (internal domain, `IRandom` seam, ArchUnit guards, 5-project test suite) |
| Project-per-layer organization | 🔵 Mostly (dedicated Contracts/SharedKernel/Infrastructure projects; Domain+Application+Endpoints still share one `Farkle` module) |

Legend: ✅ aligned · 🟡 partial · 🔵 divergent-by-design.

---

## 2. Principles comparison

| Principle | Ardalis intent | Farkle reality | Verdict |
|---|---|---|---|
| **Dependency Rule** — source dependencies point only inward, toward the domain. | Web → UseCases/Infrastructure → Core; Core depends on nothing. | Acyclic graph pointing inward: `WebApp → {Farkle, Farkle.Infrastructure} → Farkle → {Farkle.Contracts, Farkle.SharedKernel}` (see §4). `tests/Farkle.ArchitectureTests` asserts (via ArchUnitNET) that the core doesn't depend on `Farkle.Infrastructure`/the host, that domain types stay `internal`, and that the shared kernel and contracts remain dependency-free leaves. | ✅ |
| **Domain-centric design** — architecture organized around the model, not the database/framework. | Entities/aggregates/value objects/domain events in Core. | `src/Farkle/Domain/GameAggregate/` holds the `Game` aggregate, `GameState`, versioned `GameEvents`, value objects (`Score`, `GameId`, `DieValue`, `Player`, `Dice`) and `GameValidator`. The model — not the store — is the center. | ✅ |
| **Dependency inversion for infrastructure** — outer layers implement interfaces defined inward. | Repositories/services implement Core interfaces. | The read/notify **ports** `IGameViewStore` and `IGameEventBroadcaster` (in `src/Farkle/Application/`) are implemented in `Farkle.Infrastructure` (`ReadModel/EfGameViewStore.cs`, `Realtime/SignalRGameEventBroadcaster.cs`); `PortImplementationShould` asserts those implementations live in Infrastructure. The **event store** is inverted too: the core's `GameService` depends on Eventuous' `IEventStore` abstraction, and the concrete ESDB transport (`Eventuous.EventStore` + `EventStore.Client.Grpc`) is referenced and wired only in `Farkle.Infrastructure` (`Persistence/`, `AddFarkleEventStore`). | ✅ |
| **Infrastructure/DB is a detail (a plugin)** — DB/UI/framework are replaceable. | EF Core, external services confined to Infrastructure. | ESDB/Eventuous transport, Postgres (Identity + read model), SignalR and Identity are all confined to `Farkle.Infrastructure` and swappable (the storyboard test factory swaps in an in-memory store, no Docker). **Remaining caveat:** the web framework (`FastEndpoints`) is a package reference of the `Farkle` project because the HTTP **endpoints** live there, so that one framework is compiled into the core rather than plugged in around it. | 🟡 |
| **Encapsulation & rich model** — invariants enforced inside the model; no anemic entities. | Aggregates form consistency boundaries; value objects immutable. | The `Game` aggregate enforces all rules through `GameValidator` before applying events; invalid commands yield **error events** instead of mutating state. Value objects are immutable records/SmartEnums. Domain types are `internal`, enforced by `DomainPurityShould.KeepDomainTypesInternal`. | ✅ |
| **Testability** — business logic testable without DB/UI/framework. | Unit/Integration/Functional layering. | Domain unit tests mock `IRandom` and exercise the aggregate with no I/O; integration tests use Testcontainers; component tests use bUnit; E2E uses Playwright; a dedicated ArchUnitNET project guards structure. See §7. | ✅ |

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
| Shared kernel | `Ardalis.SharedKernel` package | Hand-rolled `Farkle.SharedKernel` project = infra-free domain logic (`Scoring/ScoreCalculator`, `Turns/TurnActionPolicy`), shared by server domain **and** the Blazor client |
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
Farkle                -> Farkle.Contracts, Farkle.SharedKernel
Farkle.Infrastructure -> Farkle, Farkle.Contracts
Farkle.Contracts      -> (none)
Farkle.SharedKernel   -> (none)
Farkle.ApiClient      -> (none, Kiota-generated)
Blazor.Dice           -> (none)
WebApp.Client         -> Blazor.Dice, Farkle.ApiClient, Farkle.Contracts, Farkle.SharedKernel
WebApp                -> Farkle, Farkle.Infrastructure, WebApp.Client
tests:  Farkle.Tests             -> Farkle
        Farkle.WebTests          -> Farkle, WebApp, Farkle.ApiClient
        Farkle.SpaTests          -> Farkle, WebApp.Client
        Farkle.E2eTests          -> WebApp
        Farkle.ArchitectureTests -> (loads the built assemblies to assert structure)
```

The `Farkle` project still folds **three** Clean-Architecture layers — Domain, Application (use
cases) and the HTTP Endpoints — into one assembly (separated by folder/namespace and `internal`
visibility rather than project boundary). Infrastructure, by contrast, is now its own project:

```
src/Farkle/                        ← the "core module" (Domain + Application + Endpoints)
  Domain/GameAggregate/   Game, GameState, GameEvents (V1/V2), Command,
                          GameValidator, DefaultRandomProvider, EventExtensions
  Domain/                 Validator, ValidationResult (composite-validator base)
  Application/            GameService (CommandService), GameCreator, GameIdGenerator,
                          GameViewProjector (projection logic), GameBroadcastHandler,
                          GameTelemetryHandler, GameStateSerializer, ResultExtensions,
                          CommandHandlerBuilderExtensions, IGameViewStore, IGameEventBroadcaster
                          (outbound ports); plus the feedback pipeline (FeedbackViewProjector, …)
  Endpoints/              StartGame/JoinPlayer/BeginGame/RollDice/KeepDice/PassTurn/
                          GetGameState/SubmitFeedback (FastEndpoints) + TypedEndpoint base + *Mapper.cs
  FarkleModuleServiceExtensions.cs   (DI registration — domain/application only, infra-free)
  Assembly.cs                        ([InternalsVisibleTo] for test projects)

src/Farkle.Infrastructure/         ← all server infrastructure (implements the core's ports)
  Persistence/   ESDB event store + AddFarkleEventStore + EventStoreHealthCheck
  ReadModel/     EfGameViewStore + ReadModelDbContext + projector subscription + checkpoints
  Realtime/      GameHub + SignalRGameEventBroadcaster (AddFarkleRealtime)
  Identity/      AppUser, AppDbContext, Entra data source (AddFarkleIdentity)
  Migrations/    EF Core Identity + ReadModel migrations (PostgreSQL)

src/Farkle.SharedKernel/   Scoring/ScoreCalculator (+ MachinePlayer),
                           Turns/TurnActionPolicy + GameStage
                           (pure, infra-free; shared by server domain + client)
src/Farkle.Contracts/      HttpRequests / HttpResponses (dependency-free DTOs)
```

Note that the two web helpers a reader might expect in a "shared kernel" actually live **inside**
the `Farkle` project: `TypedEndpoint` (the FastEndpoints base) in `Endpoints/`, and `ResultExtensions`
(Eventuous-`Result`→HTTP mapping) in `Application/`. `Farkle.SharedKernel` instead holds *domain*
logic (`ScoreCalculator`, `TurnActionPolicy`), which both the server aggregate and the WASM client
reference — closer to the DDD meaning of a shared kernel.

The read model is split by concern: the **projection logic** lives in the core
(`src/Farkle/Application/GameViewProjector.cs`) while its **EF persistence** and durable
subscription live in `src/Farkle.Infrastructure/ReadModel/` behind `IGameViewStore` — a clean
inversion. The `WebApp` host is now a thin composition root: `Program.cs` wires the `Farkle` module
and `Farkle.Infrastructure` together, plus the auth **endpoints/DTOs** (`Auth/`), browser telemetry
(`Telemetry/`) and Blazor Server host components (`Components/`). The Identity **persistence** it used
to own moved into `Farkle.Infrastructure/Identity/`.

### 4.3 Layer mapping

| Ardalis project | Where it lives in Farkle |
|---|---|
| `*.Core` (domain) | `src/Farkle/Domain/**` |
| `*.UseCases` (application) | `src/Farkle/Application/**` |
| `*.Web` (endpoints + composition root) | `src/Farkle/Endpoints/**` (endpoints, in the core module) + `src/WebApp/Program.cs` (composition root) + `src/WebApp/Auth/**` (auth endpoints) |
| `*.Infrastructure` | **`src/Farkle.Infrastructure/**`** — a dedicated project owning the ESDB event store (`Persistence/`), EF/Postgres read model (`ReadModel/`), SignalR (`Realtime/`) and Identity (`Identity/`, `Migrations/`). |
| `Ardalis.SharedKernel` (DDD base types) | *Package not used* (Eventuous supplies aggregate/state base types). Farkle has its **own** `Farkle.SharedKernel` project, but for **shared infra-free domain logic** — `Scoring/ScoreCalculator` and `Turns/TurnActionPolicy` (plus the shared `GameStage` enum), single sources of truth reused by the server domain *and* the Blazor client — which matches the *purpose* of a DDD shared kernel (see §5). |
| Contracts/DTOs (usually inside Web/UseCases) | `src/Farkle.Contracts/**` (extracted as its own dependency-free project) |
| *(no analog)* | `WebApp.Client` (Blazor WASM), `Farkle.ApiClient` (Kiota client), `Blazor.Dice` (reusable dice component) |

---

## 5. Building blocks

| Ardalis building block | Farkle |
|---|---|
| `Ardalis.Result` / `.AspNetCore` | ✅ Used (`Farkle.csproj`), wrapping Eventuous `Result` → HTTP in `Farkle/Application/ResultExtensions.cs` (409/404/400 mapping). |
| `Ardalis.SmartEnum` | ✅ Used for `DieValue` (`Domain/GameAggregate/GameEvents.cs`). |
| `Ardalis.GuardClauses` | 🟡 Referenced as a package; the domain favours the composite `Validator` + validation-as-events over guard clauses. |
| `Ardalis.SharedKernel` base types (`EntityBase`, `IAggregateRoot`, `DomainEventBase`, `IDomainEventDispatcher`) | ❌ Package not used — Eventuous `Aggregate<TState>` / `State<TState>` + `[EventType]` events. Farkle's own `Farkle.SharedKernel` project instead carries shared *domain* logic (`Scoring/ScoreCalculator`, `Turns/TurnActionPolicy`) reused by server + client. |
| `Ardalis.Specification` | ❌ N/A — there is no `IQueryable`/repository query surface to encapsulate under Event Sourcing. |
| `Ardalis.ApiEndpoints` | ❌ Uses **FastEndpoints** instead (which recent Ardalis templates also adopt) via `TypedEndpoint<,>` (`Farkle/Endpoints/TypedEndpoint.cs`). |
| MediatR (CQRS + domain-event dispatch) | ❌ Server uses Eventuous `CommandService`; the **client** uses BlazorState (a MediatR-like Redux loop). |
| FluentValidation | ❌ Custom composite `Validator`/`ValidationResult`; invalid commands emit `IErrorEvent`s surfaced as HTTP 400. |

---

## 6. CQRS & persistence

**Ardalis:** command/query separation is *optional* and typically realized with MediatR handlers over
EF Core; reads use repositories + specifications, often against the same database as writes.

**Farkle:** CQRS is intrinsic to the Event-Sourcing design:
- **Write side** — `GameService` (`CommandService<Game, GameState, GameId>`, in `Farkle/Application/`)
  loads the aggregate, validates, and appends events to ESDB (stream `Game-{id}`) via Eventuous'
  `IEventStore` abstraction, with optimistic concurrency. The concrete ESDB store lives in
  `Farkle.Infrastructure/Persistence/`.
- **Read side** — `GameViewProjector` (core) subscribes to the event stream and folds **one event at a
  time** onto the stored snapshot (`state.When(...)`); the durable subscription and EF persistence
  (`EfGameViewStore`/Postgres) live in `Farkle.Infrastructure/ReadModel/`, behind `IGameViewStore`.
  `GetGameStateEndpoint` serves this snapshot and **falls back to aggregate replay** when the view is
  absent (e.g. hosts without Postgres).
- **Real-time** — the broadcaster reacts to committed events and pushes updates over SignalR
  (`IGameEventBroadcaster` → `Farkle.Infrastructure/Realtime/SignalRGameEventBroadcaster.cs`).

So Farkle has *two* read paths (materialized snapshot + replay fallback) and a push channel, where the
Ardalis default has a single repository read path. Business rules are not duplicated across this split:
the pure `ScoreCalculator` and `TurnActionPolicy` (`Farkle.SharedKernel`) are the single sources of
truth — used by the aggregate on the write side and by the Blazor client for a live turn-score preview
and per-action button gating.

---

## 7. Testing layering

| Ardalis layer | Farkle equivalent |
|---|---|
| Unit (Core.Tests) | `tests/Farkle.Tests` — aggregate/validator/scoring/state/turn-policy with `IRandom` mocked. |
| Architecture guards | `tests/Farkle.ArchitectureTests` — ArchUnitNET rules: the core stays off the infra/host projects and the event-store/EF/SignalR/Identity libraries (`DependencyRulesShould`); domain types stay internal and don't reach outward (`DomainPurityShould`); port implementations live in Infrastructure (`PortImplementationShould`). |
| Integration (UseCases/Infrastructure.Tests) | `tests/Farkle.WebTests` — `WebApplicationFactory` + Testcontainers (Postgres + EventStore); HTTP contract + SignalR + Identity round-trips. |
| Functional / E2E (Web/FunctionalTests) | `tests/Farkle.E2eTests` — Playwright two-player happy path (+ a Storyboard capture using an in-memory store, no Docker). |
| *(no analog)* | `tests/Farkle.SpaTests` — bUnit component tests + BlazorState handler tests for the Blazor client. |

Farkle's pyramid is a **superset**: on top of the unit/integration/functional trio it adds a dedicated
architecture-guardrail project, plus component (bUnit) and browser (Playwright) layers because it ships
a real SPA.

---

## 8. Divergences: intentional vs. genuine gaps

**Intentional (by design — driven by the Event-Sourcing/CQRS teaching goal):**
- Event Sourcing (ESDB) instead of EF-Core state; events are the source of truth.
- Eventuous `CommandService` instead of MediatR; BlazorState on the client.
- Validation-as-events instead of throwing/FluentValidation.
- Eventuous base types instead of `Ardalis.SharedKernel`; no `Ardalis.Specification` (no query surface).
- A single `Farkle` *module* project for Domain + Application + Endpoints in a small,
  single-bounded-context game, separating those layers by folder + `internal` visibility rather than
  by project. (Infrastructure, Contracts and the SharedKernel *are* separate projects.)
- Scoring and the turn-action rule extracted into an infra-free `Farkle.SharedKernel`
  (`ScoreCalculator`, `TurnActionPolicy`) reused by both the server domain and the Blazor client — a
  Clean-Architecture-friendly shared kernel (one source of truth, no duplicated rules between back end
  and UI).

**Genuine gaps (would hold even if the stack were EF + MediatR):**
- **HTTP endpoints share the project with the domain** (`Endpoints/` next to `Domain/` in `Farkle`),
  so a Web concern — and the `FastEndpoints` package — is compiled into the core module. Ardalis keeps
  endpoints in a separate `*.Web` project so the core is web-framework-free. This is the main remaining
  packaging divergence from the canon.
- The core still carries some framework-adjacent package references beyond the domain's needs
  (OpenAPI/Swagger tooling for endpoint generation, messaging/config clients), a side effect of the
  endpoints and DI wiring living in the same assembly.

**Closed since earlier revisions of this document (PR #197):**
- ~~No dedicated Infrastructure project~~ → `Farkle.Infrastructure` now owns the event store, EF read
  model, SignalR and Identity.
- ~~The core compiles against `EventStore.Client.Grpc` / `Eventuous.EventStore`~~ → those moved to
  `Farkle.Infrastructure`; the core references only the Eventuous *abstractions* (by design, like
  `Ardalis.SharedKernel` would be).
- ~~The ArchUnit rule is effectively empty / aspirational~~ → `tests/Farkle.ArchitectureTests` now
  actively forbids the core from depending on the infrastructure stacks and the host, keeps domain
  types internal, and pins port implementations to Infrastructure.

These observations are recorded neutrally; given Farkle's purpose as an ES/CQRS sample, the remaining
gaps are not defects to "fix" — they simply mark where the packaging still differs from the Ardalis
canon.

---

## Appendix — sources for the Ardalis canon

- Ardalis Clean Architecture template — https://github.com/ardalis/CleanArchitecture
- "Clean Architecture with ASP.NET Core" — https://ardalis.com/clean-architecture-asp-net-core/
- `Ardalis.SharedKernel` — https://www.nuget.org/packages/Ardalis.SharedKernel
- `Ardalis.Result` — https://www.nuget.org/packages/Ardalis.Result
- `Ardalis.Specification` — https://www.nuget.org/packages/Ardalis.Specification
- eShopOnWeb reference app — https://github.com/MicrosoftLearning/eShopOnWeb
- .NET microservices architecture guide — https://learn.microsoft.com/dotnet/architecture/microservices/
