# Farkle vs. Ardalis Clean Architecture

A descriptive, side-by-side comparison of Farkle's architecture with the canonical
[Ardalis Clean Architecture](https://github.com/ardalis/CleanArchitecture) template — and, more
importantly, **where Farkle deliberately diverges from Clean/Onion** now that it's built as
**vertical slices on the Critter Stack** (Marten + Wolverine).

> **Scope & stance.** This is an *analysis*, not a proposal. After the #295 migration (ADR 0004)
> Farkle is an Event-Sourcing / CQRS sample organized as **vertical slices around a shared aggregate
> kernel**, on **Marten + Wolverine + PostgreSQL**. That is a deliberate departure from both Ardalis's
> default **EF Core + MediatR** stack *and* from horizontal Clean/Onion layering. Divergences are
> described as intentional trade-offs; nothing here recommends a refactor.

> **Currency.** This reflects the repo **after the Critter Stack migration** (#301–#305): no Eventuous,
> no EventStoreDB, no FastEndpoints, no `Farkle.Endpoints`/`Farkle.Application` horizontal layers.
> Slices live in `src/Farkle/Features/`; the domain is Marten-native. See
> [`critter-stack-onboarding.md`](critter-stack-onboarding.md) and the [`decisions/`](decisions/) ADRs.

---

## 1. TL;DR verdict

Farkle honours the **spirit** of Clean Architecture where it still applies — the shared kernel is a
pure, framework-light core; the dependency graph is acyclic; the model is rich and encapsulated; the
test pyramid is strong — but it **deliberately drops horizontal layering** in favour of vertical
slices, and it **embraces the framework** rather than inverting it behind ports. There is no
`*.UseCases`/`*.Application` layer and no repository/mediator wrapper over Marten/Wolverine; a feature
lives in one folder and talks to the framework directly. The one pure thing per feature — the
**decider** — is kept clean by an **arch-test, not a project boundary**.

| Principle | Verdict |
|---|---|
| Dependency Rule (point inward) | ✅ Aligned for the shared kernel (acyclic inward graph, ArchUnit-enforced) |
| Domain-centric, rich model | ✅ Aligned (the aggregate/snapshot enforces invariants; no anemic models) |
| Dependency inversion for infrastructure | 🔵 **Divergent by design** — slices use Marten/Wolverine directly; only genuinely swappable concerns (SignalR, Identity) sit behind ports in `Farkle.Infrastructure` |
| "Infrastructure/DB is a detail (plugin)" | 🔵 **Divergent by design** — the event store is *not* hidden behind a port; Marten *is* the domain's persistence (ADR 0004) |
| Encapsulation / testability | ✅ Aligned (pure deciders + `IRandom` seam; ArchUnit guards; 6-project test suite) |
| Horizontal project-per-layer | 🔵 **Divergent by design** — vertical slices in `Features/`, not `*.Core`/`*.UseCases`/`*.Infrastructure` for features |

Legend: ✅ aligned · 🟡 partial · 🔵 divergent-by-design.

---

## 2. Stack at a glance

| Concern | Ardalis default | Farkle |
|---|---|---|
| Persistence | EF Core (state-based) + repositories | **Marten** event sourcing on PostgreSQL; one store for events + the `GameState` snapshot |
| Write model | Entities saved through `IRepository<T>` | Pure **decider** `Decide(command, state) → events`; events appended to stream `game-{id}` |
| Command dispatch | MediatR command handlers | **Wolverine.HTTP** endpoint calls the decider directly (`[WriteAggregate]` loads the aggregate); no mediator |
| Query/read side | Repository + `Ardalis.Specification` | Marten **`Inline` self-aggregating snapshot** (`GameState`) read via `IQuerySession`; no daemon |
| HTTP layer | `Ardalis.ApiEndpoints` / FastEndpoints / Minimal API | **Wolverine.HTTP** static endpoint methods, one per slice |
| Validation | FluentValidation + `Ardalis.GuardClauses` | Composite `Validator` primitives + **validation-as-events** (`IErrorEvent` → 400 `ProblemDetails`) |
| Result pattern | `Ardalis.Result` | `Ardalis.Result` on the client adapter; endpoints return `Results<Ok<T>, ProblemHttpResult>` tuples |
| Domain base types | `Ardalis.SharedKernel` | None — `GameState` is a plain Marten self-aggregating snapshot (`Create`/`Apply`) |
| Shared kernel | `Ardalis.SharedKernel` package | Hand-rolled `Farkle.SharedKernel` = infra-free domain logic (`ScoreCalculator`, `TurnActionPolicy`), shared by server **and** the Blazor client |
| Real-time | (none in template) | SignalR pushed via the Marten/Wolverine **outbox** (cascaded `GameNotifications`) |
| Client | (none in template) | Blazor WASM (`WebApp.Client`) + BlazorState; Kiota client (`Farkle.ApiClient`) |

---

## 3. Code organization

### 3.1 Ardalis canonical layout (horizontal)
```
src/
  *.Core            (domain)                         — depends on nothing
  *.UseCases        (application: MediatR handlers)  — depends on Core
  *.Infrastructure  (EF Core, repositories)          — depends on Core (+ UseCases)
  *.Web             (endpoints; composition root)    — depends on UseCases + Infrastructure
```
Dependency direction: `Web → {UseCases, Infrastructure} → Core`.

### 3.2 Farkle actual layout (vertical slices around a shared kernel)
```
Farkle                -> Farkle.Contracts, Farkle.SharedKernel   (Marten + Wolverine embraced here)
Farkle.Contracts      -> (none)                                   (dependency-free DTO leaf)
Farkle.SharedKernel   -> (none)                                   (pure; shared with the WASM client)
Farkle.Infrastructure -> Farkle, Farkle.Contracts                (SignalR + Identity only)
Farkle.ApiClient      -> (none, Kiota-generated)
WebApp                -> Farkle, Farkle.Infrastructure, WebApp.Client   (composition root + static codegen)
```
Inside `src/Farkle/`:
```
Features/<Command>/        ← a vertical slice: <Command>Decider.cs (pure) + <Command>Endpoint.cs (Wolverine.HTTP)
Features/Responses/        ← GameState→DTO mappers
Features/GameNotifications.cs  ← cascaded outbox notifications (the broadcast vocabulary)
Domain/GameAggregate/      ← the SHARED AGGREGATE KERNEL: GameState (Marten snapshot), GameEvents (V1/V2),
                             Command, GameValidator, value objects — legitimately shared by every slice
Application/               ← GameCreator, GameNotifier, GameBroadcastHandler, GameTelemetryHandler, feedback
```
There is **no `Farkle.Application` or `Farkle.Endpoints` layer for features** — a change to one behaviour
touches one `Features/<Command>/` folder. The only cross-slice code is the shared kernel: `GameState`, the
event vocabulary, value objects, and `Farkle.SharedKernel`. Event sourcing forces one stream/state, so
sharing that *vocabulary* is legitimate — it is not the horizontal scatter VSA fights.

### 3.3 What still inverts (and why)
Not everything is embraced-in-place. **`Farkle.Infrastructure`** keeps the two concerns that are genuinely
swappable and don't belong in a slice: **SignalR** (`Realtime/SignalRGameEventBroadcaster` behind the
broadcast chain) and **ASP.NET Identity** (`Identity/`, its own EF `AppDbContext` + migrations). These are
the only ports left — the event store is *not* one (that's the ADR 0004 decision).

---

## 4. Where Farkle deliberately diverges from Clean/Onion

This is the heart of the comparison post-migration. Each divergence is a conscious trade the Critter Stack
and VSA ask for; we accept the cost noted.

| Clean/Onion tenet | Farkle's choice | Why we accept the trade |
|---|---|---|
| **Depend on abstractions; invert infrastructure behind ports** | The domain references Marten (`GameState` is a Marten aggregate); slices use `IDocumentSession`/`[WriteAggregate]`/`IQuerySession` directly. | Marten *is* the event-sourcing model; a repository over it would re-implement Marten worse and hide the one capability we chose the stack for. The cost — the core references a persistence library — is bounded by the arch-tests (EF/SignalR/web frameworks stay out). |
| **No framework types in the application/domain** | Wolverine.HTTP endpoints and the outbox live inside slices; the domain package-references Marten/Wolverine/Npgsql. | Low ceremony is the point. Purity is preserved exactly where it pays off — the **decider** — and enforced by `KeepDecidersPureAndFrameworkFree`, not by a project wall. |
| **Horizontal layers (Core/UseCases/Infrastructure)** | Vertical slices; one folder per feature. | A feature is understood and changed in one place; there is no "spread the change across four projects." The shared kernel is the deliberate, minimal exception. |
| **Mediator decouples request from handler** | The Wolverine.HTTP **endpoint is the handler**; it calls the pure decider directly (no message dispatch for commands). | One less indirection for a single-process app; Wolverine still provides the outbox/messaging where it earns its keep (post-commit broadcast + telemetry). |
| **The dependency rule holds for every project** | It holds for the **shared kernel and the leaves** (Contracts, SharedKernel), and for keeping EF/SignalR/web out of the core — but slices intentionally point "outward" at Marten/Wolverine. | The rule that matters (don't let the *web/EF/UI* leak into decision logic) is kept and tested; the rest is traded for locality. |
| **Enforce boundaries with project references** | Boundaries are enforced by **arch-tests** (ArchUnitNET), not project structure. | Colocation (decider next to its endpoint) beats a project boundary for readability; the test suite makes the boundary just as real. |

The guardrails that make these trades safe (`tests/Farkle.ArchitectureTests/`): **decider purity**
(`KeepDecidersPureAndFrameworkFree`), **slices point inward only** (`KeepSlicesOffTheInfrastructureAndHostLayers`
— slice → shared kernel/app allowed, slice → infra/host forbidden), **core free of EF/SignalR/Identity**
(`KeepTheCoreFreeOfInfrastructureLibraries`, while Marten/Wolverine/Npgsql are permitted), and the
**shared kernel + contracts stay dependency-free leaves**.

---

## 5. Building blocks

| Ardalis building block | Farkle |
|---|---|
| `Ardalis.Result` / `.AspNetCore` | ✅ Used on the client `IGameService` adapter; server endpoints return `Results<Ok<T>, ProblemHttpResult>`. |
| `Ardalis.SmartEnum` | ✅ Used for `DieValue`. |
| `Ardalis.GuardClauses` | 🟡 Referenced; the domain favours composite validators + validation-as-events. |
| `Ardalis.SharedKernel` base types | ❌ Not used — `GameState` is a plain Marten self-aggregating snapshot. Farkle's own `Farkle.SharedKernel` carries *shared domain logic* (`ScoreCalculator`, `TurnActionPolicy`) reused by server + client. |
| `Ardalis.Specification` | ❌ N/A — no `IQueryable`/repository surface under event sourcing; reads go to the Marten snapshot. |
| `Ardalis.ApiEndpoints` / FastEndpoints | ❌ Uses **Wolverine.HTTP** static endpoint methods. |
| MediatR | ❌ Server: Wolverine (endpoint-as-handler + outbox). Client: BlazorState (a MediatR-like Redux loop). |
| FluentValidation | ❌ Composite `Validator` + validation-as-events (`IErrorEvent` → 400). |

---

## 6. CQRS & persistence

CQRS is intrinsic to the event-sourcing design:
- **Write side** — a Wolverine.HTTP endpoint loads `GameState` via `[WriteAggregate]`, calls the pure
  decider, and returns the events for Marten to append to stream `game-{id}` (optimistic concurrency via
  `FetchForWriting`). No repository, no command service.
- **Read side** — `GameState` is a Marten **`Inline`** self-aggregating snapshot (updated in the same
  transaction as the append — read-your-own-writes), read directly via `IQuerySession`. No async daemon,
  no replay fallback, no separate read database.
- **Real-time** — a slice returns a `GameNotifications.*`; Wolverine publishes it through the Marten
  **outbox** after commit → `GameBroadcastHandler` → `GameNotifier` → SignalR group `game-{id}`.

Business rules are not duplicated across the split: the pure `ScoreCalculator` and `TurnActionPolicy`
(`Farkle.SharedKernel`) are the single sources of truth, used by the server decider and by the Blazor
client for a live turn-score preview + per-action button gating.

---

## 7. Testing layering

| Ardalis layer | Farkle equivalent |
|---|---|
| Unit (Core.Tests) | `tests/Farkle.Tests` — pure **decider** tests (`(command, state) → events` via `GameState.Fold`), scoring, turn policy. |
| Architecture guards | `tests/Farkle.ArchitectureTests` — ArchUnitNET: decider purity, slices inward-only, core off EF/SignalR/web, dependency-free leaves. |
| Integration | `tests/Farkle.WebTests` — **Alba + TrackedSession** on real Postgres (Testcontainers); HTTP contract + Marten round-trip + outbox broadcast + Identity/JWT. |
| Functional / E2E | `tests/Farkle.E2eTests` — Playwright two-player happy path (+ a Storyboard capture). |
| *(no analog)* | `tests/Farkle.SpaTests` — bUnit component + BlazorState handler tests; `tests/Blazor.Dice.Tests`. |

Farkle's pyramid is a **superset**: on top of unit/integration/functional it adds a dedicated
architecture-guardrail project plus component (bUnit) and browser (Playwright) layers because it ships a real SPA.

---

## Appendix — sources for the Ardalis canon

- Ardalis Clean Architecture template — https://github.com/ardalis/CleanArchitecture
- "Clean Architecture with ASP.NET Core" — https://ardalis.com/clean-architecture-asp-net-core/
- `Ardalis.Result` — https://www.nuget.org/packages/Ardalis.Result
- The Critter Stack (Marten + Wolverine) — https://jasperfx.net/
- Vertical Slice Architecture — https://www.jimmybogard.com/vertical-slice-architecture/
