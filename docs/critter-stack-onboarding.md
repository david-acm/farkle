# Critter Stack onboarding

> **Read this once and you can add a feature to Farkle.** It explains how the app is
> organized (vertical slices around a shared aggregate kernel), the mindset the
> [Critter Stack](https://jasperfx.net/) (Marten + Wolverine) asks for, the day-to-day
> decision rules, and an end-to-end walkthrough of adding a slice.
>
> Farkle is a teaching sample. After the #295 migration it teaches the Critter Stack the way
> the stack itself recommends: low-ceremony, framework-embracing slices with a pure decision
> core. This doc is the map; [`CLAUDE.md`](../CLAUDE.md) is the reference; the
> [`docs/decisions/`](decisions/) ADRs are the "why".

---

## 1. Slice anatomy — open one folder

A feature lives in **one folder** under `src/Farkle/Features/<Command>/`. Everything for the
use case is colocated: the command, the pure decider, the Wolverine.HTTP endpoint, the response
mapping, and (if it broadcasts) the cascaded notification. Its tests live next to their layer —
the pure decider test in `tests/Farkle.Tests/Features/<Command>/`, the HTTP/broadcast test in
`tests/Farkle.WebTests/Slices/`.

### The shape of a mutating slice

Take `PassTurn` (`src/Farkle/Features/PassTurn/`):

```csharp
// PassTurnDecider.cs — PURE. (command, state) -> events. No framework types.
internal static class PassTurnDecider
{
  public static IEnumerable<object> Decide(PassTurnCommand command, GameState state)
  {
    if (!new PlayerIsInTurn(state, command.PlayerId).IsSatisfied())
      return [new GameEvents.V1.PlayedOutOfTurn(command.GameId, command.PlayerId)];
    // …turn-action policy check…  emits V1.TurnPassed (+ V1.GameWon past the winning score)
  }
}
```

```csharp
// PassTurnEndpoint.cs — the endpoint IS the handler (ADR 0004, Option C).
public static string StreamId(int gameId) => $"game-{gameId}";

[WolverinePost("/api/games/{gameId:int}/players/{playerId:int}/turns")]
public static (Results<Ok<PassTurnResponse>, ProblemHttpResult>, Events, GameNotifications.TurnChanged?) Post(
  int gameId, int playerId,
  [WriteAggregate(FromMethod = nameof(StreamId))] GameState state)   // Marten loads the "game-{id}" stream
{
  var events = PassTurnDecider.Decide(new PassTurnCommand(gameId, playerId), state).ToArray();

  // validation-as-events -> HTTP 400 ProblemDetails, append nothing, broadcast nothing
  if (events.OfType<IErrorEvent>().FirstOrDefault() is { } error)
    return (TypedResults.Problem(statusCode: 400, title: error.GetType().Name), new Events(), null);

  var response = PassTurnMapper.ToPassTurnResponse(GameState.Fold(state, events), playerId);
  return (TypedResults.Ok(response), new Events(events), new GameNotifications.TurnChanged(gameId, playerId));
}
```

Read that endpoint top to bottom — it is the whole pattern:

1. **`[WriteAggregate(FromMethod = nameof(StreamId))] GameState state`** — Wolverine's Marten
   integration loads the current `GameState` snapshot for stream `game-{gameId}` and hands it in.
   You never call `IDocumentSession` yourself in a mutating slice.
2. **Call the decider directly.** The command is constructed inline and passed to the pure
   `Decide(...)`. Commands are *not* dispatched as Wolverine messages — the endpoint is the handler.
3. **Return a tuple.** Its shape is load-bearing:
   - `Results<Ok<TResponse>, ProblemHttpResult>` — the HTTP result (200 body or a 400 problem).
   - `Events` — Wolverine appends these to the stream (`new Events(events)` on success, an **empty
     `new Events()`** on validation failure so nothing is written).
   - `GameNotifications.X?` — an optional cascaded notification published **through the Marten
     outbox after the append commits** (`null` = no broadcast).
4. **Compute the response by folding, not re-reading.** `GameState.Fold(state, events)` replays the
   just-produced events over the loaded state in-memory; the endpoint never re-queries Marten.

### The two exceptions

- **`StartGame`** has no existing stream, so it has no `[WriteAggregate]`. It delegates to
  `IGameCreator`, which `StartStream`s a fresh `game-{id}` with collision-retry (`GameCreator.cs`).
  It broadcasts nothing (no players yet).
- **`GetGame`** and **`Feedback`** are read/append-only and have **no decider** — just an endpoint.
  `GetGame` reads the `GameState` snapshot straight off Marten by key (`IQuerySession`).

Every mutating slice (`JoinPlayer`, `BeginGame`, `RollDice`, `KeepDice`, `SetDiceAside`,
`ReturnDice`, `PassTurn`) follows `PassTurn` exactly, differing only in the decider, the response
mapper, and which `GameNotifications.*` it returns. (`RollDice` also injects `IRandom`.)

### The broadcast chain (cascaded notification → SignalR)

When a slice returns a `GameNotifications.*` record, Wolverine publishes it through the Marten
**outbox** once the event append commits, and this handler chain runs:

```
Features/GameNotifications.cs            (LobbyChanged, GameBegan, DiceRolled, TableChanged, TurnChanged)
  -> Application/GameBroadcastHandler.cs (Wolverine Handle(...) per notification)
  -> Application/GameNotifier.cs         (reloads the fresh GameState via IQuerySession, then pushes it
                                          straight through IHubContext<GameHub> — no port, ADR 0005)
  -> SignalR group "game-{id}"           (GameHub lives in Farkle/Realtime/, in the core)
```

The client (`WebApp.Client`) listens on the SignalR hub and dispatches a BlazorState action so
every player's UI updates live. Because the broadcast is outbox-driven, it only fires for events
that actually committed — no phantom broadcasts on a rolled-back request.

---

## 2. Mindset — embrace the framework

The Critter Stack rewards *low ceremony*. The rules the domain used to hide behind ports are now
expressed directly against Marten and Wolverine, and the one thing we keep pure is the decision.

- **No repository over Marten, no mediator over Wolverine, no ports we'll never swap.** Slices use
  `IDocumentSession` / `IQuerySession` / `[WriteAggregate]` directly. There is no `IGameRepository`,
  no `CommandService`, no `IAggregateStore`. If you're writing an interface whose only implementer
  wraps Marten or Wolverine, stop.
- **The decider is the one pure thing.** `Decide(command, state) → events` references only the
  shared kernel — no Marten, Wolverine, ASP.NET, or Npgsql. It's a total function you can unit-test
  with zero I/O. This is enforced by an **arch-test, not a project boundary** (see §6): the decider
  lives *inside* the framework-coupled slice for locality, and `KeepDecidersPureAndFrameworkFree`
  guarantees it stays clean.
- **The domain embraces Marten.** Post-#302 (ADR 0004) `GameState` *is* the Marten aggregate and read
  model — a self-aggregating `Inline` snapshot with conventional `Create`/`Apply` methods. The domain
  is allowed to reference Marten/Wolverine/Npgsql; the guardrail forbids the *web/EF/SignalR*
  frameworks and outward dependencies, not the Critter Stack itself.
- **Vertical slices over horizontal layers.** There is no `Farkle.Application` layer *for features* and
  no `Farkle.Endpoints` project — a change to one behaviour touches one folder. The only cross-slice
  code is the **shared aggregate kernel**: `GameState`, the event vocabulary, value objects, the
  validator primitives, and `Farkle.SharedKernel` (`ScoreCalculator`, `TurnActionPolicy`). Sharing
  *vocabulary* is not the scatter VSA fights — event sourcing forces one stream/state, so all slices
  legitimately share it.
- **Validation-as-events, surfaced once.** A broken rule is a domain fact: the decider returns an
  `IErrorEvent`, the endpoint maps the first one to a 400 `ProblemDetails`, and nothing is appended.
  No duplicate rule in HTTP middleware.

---

## 3. Dev heuristics — decision rules

| Situation | Do this |
|---|---|
| **New behaviour** | Create or point at a `Features/<Command>/` folder. Write the **pure decider test first** (`(command, state) → events`), then `Decide`, then the endpoint. |
| **The decider needs an input the command doesn't carry** (a dice roll, the clock) | Inject the dependency into the **endpoint** and pass the resolved value into `Decide` (e.g. `RollDice` injects `IRandom`, rolls, and hands the dice to the decider). Keep `Decide` pure and deterministic. |
| **You need cross-slice data** | Don't reach into another slice. Go through the **shared kernel** (`GameState`, validator primitives, `ScoreCalculator`, `TurnActionPolicy`). `KeepSlicesOffTheInfrastructureAndHostLayers` and the decider-purity rule enforce this. |
| **A test needs a messaging side effect** (broadcast, outbox) | Use **`TrackAsync` (TrackedSession)** in `Farkle.WebTests` and assert on `tracked.Executed.SingleMessage<GameNotifications.X>()`. Never `Task.Delay`/poll. Plain Alba (no tracking) for pure request/response. |
| **A read-model change** | Read the `GameState` snapshot via `IQuerySession` (it's an `Inline` projection — read-your-own-writes, no daemon). Never query the write side. |
| **An event's shape must change** | **Add a new version** (`GameEvents.V2.*`) and a matching `Apply`/`Handle` — never mutate a stored `V1` schema. `GameState` registers handlers for both. |
| **A business rule is violated** | Return the `IErrorEvent` from the decider. The endpoint turns the first one into a 400 `ProblemDetails`. Do not add the rule again in middleware or the client. |
| **You changed a contract** (`Farkle.Contracts` DTO or a route) | Regenerate the OpenAPI doc + Kiota client and commit them (CI `verify-generated` enforces it). See [`api-client-generation.md`](api-client-generation.md). |
| **You changed a handler/endpoint signature** | Regenerate the static codegen and commit it: `dotnet run --project src/WebApp --no-launch-profile -- codegen write` (CI `verify-codegen` enforces it). |

---

## 4. Do / Don't

| ✅ Do | ❌ Don't |
|---|---|
| Keep `Decide` a pure `(command, state) → events` function | Put `IDocumentSession`, `HttpContext`, or `DateTime.Now` in a decider |
| Load the aggregate with `[WriteAggregate(FromMethod = nameof(StreamId))]` | Hand-roll `session.Events.FetchForWriting` in a mutating endpoint |
| Return `new Events(events)` on success, `new Events()` (empty) on a validation error | Append events and *then* try to signal failure |
| Compute the response via `GameState.Fold(state, events)` | Re-read Marten after the append to build the response |
| Broadcast by returning a `GameNotifications.*` from the endpoint (outbox-driven) | Call the SignalR hub directly from a slice |
| Reach shared behaviour through the kernel (`ScoreCalculator`, `TurnActionPolicy`) | Reference another slice, or add a horizontal `Application`/`Endpoints` layer |
| Add a `V2` event for a schema change | Edit a stored `V1` record's shape |
| Surface a broken rule as an `IErrorEvent` → 400 | Re-implement the rule in middleware or the Blazor client |

---

## 5. Tooling cheatsheet (JasperFx CLI)

The host entrypoint is `return await app.RunJasperFxCommands(args);`, so the JasperFx CLI runs
against the real configuration:

```bash
dotnet run --project src/WebApp -- describe            # print the resolved Marten/Wolverine config
dotnet run --project src/WebApp -- resources list      # list resources (endpoints, subscriptions…)
dotnet run --project src/WebApp -- codegen write       # (re)write the static handler/endpoint code
dotnet run --project src/WebApp -- codegen test        # compile every handler/endpoint (CI + a WebTest use this)
dotnet run --project src/WebApp -- db-apply            # apply the Marten schema
dotnet run --project src/WebApp -- projections rebuild  # rebuild projections
```

**Codegen: dev-fast, prod-static.** Generated Wolverine handler/endpoint code lives in
`src/WebApp/Internal/Generated/`. Real **Production** loads it (`TypeLoadMode.Static`,
`AssertAllPreGeneratedTypesExist`) for a fast, Roslyn-free cold start. **Development**, tests, and the
OpenAPI `GetDocument` boot (the `NSwag` environment) stay **Dynamic** (regenerated in-memory), so they
never depend on the committed code being current. `opts.ApplicationAssembly = typeof(Program).Assembly`
in `AddJasperFx` tells JasperFx the generated code lives in the **WebApp** host assembly. After changing
a handler/endpoint signature, run `codegen write` and commit — `verify-codegen` CI fails on drift.

**Schema.** Marten manages its own schema (`AutoCreate.CreateOrUpdate` — no hand-written event/projection
migrations). ASP.NET **Identity keeps its EF migrations**. See [`../infra/OPERATIONS.md`](../infra/OPERATIONS.md).

---

## 6. Guardrails (arch-tests)

`tests/Farkle.ArchitectureTests/` (ArchUnitNET) load the built assemblies and assert the model. The
ones you'll bump into:

- **`KeepDecidersPureAndFrameworkFree`** — a `*Decider` must not touch Marten, Wolverine, ASP.NET,
  FastEndpoints, Npgsql, or Eventuous. Purity by test, not by boundary.
- **`KeepSlicesOffTheInfrastructureAndHostLayers`** — a slice may front the application layer
  (`IGameCreator`, `GameNotifier`) but must not reach Infrastructure or the host.
- **`KeepTheCoreFreeOfInfrastructureLibraries`** — the core must not reference EF Core, SignalR, or
  Identity. (Marten + Wolverine + Npgsql *are* deliberately allowed in the core — ADR 0004.)
- **`KeepTheSharedKernelFreeOfTheWebFramework`** / **`KeepTheSharedKernelPureAndDependencyFree`** — the
  shared kernel is referenced by the WASM client too, so it stays off every server framework.
- **`KeepContractsAsADependencyFreeLeaf`**, **`KeepTheBlazorClientOffTheServerCoreAndInfrastructure`**.

Theme: **deciders are pure**, **slices point inward only** (slice → shared kernel/app allowed;
slice → infra/host forbidden), and the **core embraces Marten/Wolverine while staying off EF/SignalR/web**.

---

## 7. Walkthrough — add a slice end-to-end

Say you're adding a `ForfeitGame` command. (This replaces the old FastEndpoints "add an endpoint"
recipe.)

1. **Folder.** `src/Farkle/Features/ForfeitGame/`.
2. **Command.** Add `ForfeitGameCommand(GameId GameId, PlayerId PlayerId)` as `Features/ForfeitGame/ForfeitGameCommand.cs` — the command lives in the slice that owns it.
3. **Event(s).** Add `V1.GameForfeited(int GameId, int PlayerId)` to `GameEvents.cs`, and a
   `HandleGameForfeited` + a `GameState.Apply(V1.GameForfeited)` overload *and* a `Fold` case in
   `GameState.cs` (so both Marten replay and the pure `Fold` see it). If it's an error path, mark the
   error record `: IErrorEvent` (no `Apply` — error events are inert on replay).
4. **Decider test (red).** In `tests/Farkle.Tests/Features/ForfeitGame/ForfeitGameDeciderShould.cs`,
   arrange state with `GameState.Fold(...events)` and assert the events `Decide` emits.
5. **Decider (green).** `ForfeitGameDecider.Decide(command, state)` — pure, kernel-only, returns the
   event or an `IErrorEvent`.
6. **Endpoint.** `ForfeitGameEndpoint` with `StreamId(int) => $"game-{id}"`, `[WolverinePost(...)]`,
   `[WriteAggregate(FromMethod = nameof(StreamId))] GameState state`, the error→400 check, and a tuple
   return `(result, new Events(events), notification?)`.
7. **Response + mapper.** Add the DTO to `Farkle.Contracts/HttpResponses.cs` and a mapper under
   `Features/Responses/` if the shape is non-trivial.
8. **Broadcast (optional).** If other players should see it, add a `GameNotifications.GameForfeited`
   record and return it from the endpoint; add a `Handle(...)` to `GameBroadcastHandler` and a client
   listener.
9. **Integration test.** `tests/Farkle.WebTests/Slices/ForfeitGameShould.cs` via the Kiota client;
   use `TrackAsync` if it broadcasts.
10. **Regenerate.** `codegen write` (new endpoint) and, if the contract changed, the OpenAPI + Kiota
    client. Commit both — CI's `verify-codegen` and `verify-generated` enforce them.

That's the whole loop: **folder → command → event + state handler → decider (test-first) → endpoint →
response → (broadcast) → integration test → regenerate.**

---

## Where to look next

- [`../CLAUDE.md`](../CLAUDE.md) — the full architecture + commands reference.
- [`decisions/`](decisions/) — the ADR log (why the stack, stream identity, Marten-native domain, codegen).
- [`architecture-vs-ardalis-clean-architecture.md`](architecture-vs-ardalis-clean-architecture.md) —
  where vertical slices + the Critter Stack deliberately diverge from Clean/Onion.
- [`api-client-generation.md`](api-client-generation.md) — regenerating the OpenAPI doc + Kiota client.
- [`remote-sessions.md`](remote-sessions.md) — SDK/Chromium setup for Claude Code web/remote sessions.
