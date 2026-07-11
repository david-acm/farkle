# 4. Go fully Marten-native: GameState is the aggregate; embrace the framework

Status: **Accepted** (#302). Supersedes ADR 0001 and ADR 0003.

## Context

ADR 0001 kept the domain **framework-free** — no Marten/Wolverine reference in `Farkle`,
purity enforced by an architecture test — and expressed command logic as pure `Decide`
deciders. ADR 0003 then had to bridge that pure domain to Marten with a `GameDocument`
**wrapper** (Marten-native string id + the pure `GameState` nested inside) folded by a
**manual `Evolve`** override, because Marten 9's aggregation source generator (which
discovers conventional `Create`/`Apply` methods) only runs in the assembly that references
the analyzer — and `Farkle` deliberately did not.

Reviewing that shape against how the Critter Stack is actually meant to be used, the wrapper
and the manual `Evolve` exist **only** to preserve a purity boundary the framework's authors
explicitly discourage:

- Marten's author calls hiding the persistence tool behind repository/abstraction layers
  **harmful**, and recommends using Marten's APIs **directly** in handlers/endpoints.
- The recommended single-stream pattern is a **self-aggregating snapshot**: conventional
  `Create`/`Apply` methods **on the aggregate type itself**.
- Wolverine's **`[AggregateHandler]`** workflow loads the stream via `FetchForWriting`,
  projects it through the aggregate's own `Apply` methods, hands it to the handler, and
  appends the returned events — and this *is* the Critter Stack's "decider."

So the framework-free guardrail was fighting the grain, and the wrapper/`Evolve` were the
cost of that fight.

## Decision

**Reference Marten (and its source-gen analyzer) from `Farkle` and make `GameState` a
Marten-native self-aggregating snapshot.** Embrace the framework directly.

1. **`GameState` is the aggregate.** It carries a Marten-native `string` id (the
   `"game-{code}"` stream key, ADR 0002) and conventional, `public` `Create(GameStarted)` /
   `Apply(<Event>)` methods. These **replace** `GameState.Fold`, the `GameProjection`, the
   `GameDocument` wrapper, and the manual `Evolve` from ADR 0003 — all removed.
2. **Commands are handled by thin `[AggregateHandler]` methods that keep the #301 deciders.**
   One handler per slice, `(Result<TResponse>, Events) Handle(Command.X cmd, GameState state, <deps>)`:
   it calls the pure `XDecider.Decide(...)` (kept unchanged, unit-tested with no mocks), appends the
   returned events, and returns an Ardalis `Result` (an `Error` carrying an `IErrorEvent` name maps to
   HTTP 400). `RollDice` rolls `IRandom` in the handler *before* calling the decider so the decision
   stays pure. Stream identity is resolved by convention — see **Handler identity** below. `StartGame`
   is a plain `StartStream` handler (it creates the stream, so there is no aggregate to fetch).
3. **No wrapper document, no separate read projection.** `GameState` is both the write-side
   snapshot (via `FetchForWriting`) and the read model; the `GetGame` slice queries it with
   `IQuerySession.LoadAsync`/`FetchLatest` and maps it to the `GameView` DTO. Registered
   **Inline** for read-your-own-writes.
4. **Collapse `Farkle.Features` into `Farkle`.** With the domain referencing Marten there is
   no purity boundary left to justify a second project; the Marten-aware vertical slices and
   `AddFarkleCritterStack` live in `Farkle`.
5. **Drop the framework-free architecture tests** (`KeepDecidersPureAndFrameworkFree` and the
   slice-isolation rule that forbade infrastructure references). Slice cohesion guardrails
   that do not depend on the purity boundary are kept.

### Shape (validated by the same spike as ADR 0003)

```csharp
// GameState.cs — the Marten aggregate (self-aggregating snapshot)
public record GameState
{
    public string Id { get; init; } = "";           // Marten-native id == the "game-{code}" stream key
    // … Players, TableCenter, DiceKept, TurnScore, ScoreTable, StraightsKeptThisTurn, Winner, GameStage …

    public static GameState Create(GameStarted e) => new() { Id = $"game-{e.GameId}", GameStage = Rolling };
    public GameState Apply(DiceRolled e) => this with { TableCenter = e.Dice, GameStage = Keeping };
    // … one Apply per event; V1→V2 upcasting handled at registration …
}

// StartGame/StartGameHandler.cs — the handler IS the decider
public static class StartGameHandler
{
    public static IEnumerable<object> Handle(Command.StartGame cmd, GameState state) =>
        /* #301 StartGameDecider body, verbatim */;
}

// Registration (in Farkle)
services.AddMarten(opts =>
{
    opts.Connection(connectionString);
    opts.Events.StreamIdentity = StreamIdentity.AsString;         // "game-{code}"
    opts.Projections.Snapshot<GameState>(SnapshotLifecycle.Inline);

    // Verified by spike: STJ + the SmartEnum value converter is required for GameState to
    // round-trip as a stored document. Without it, ImmutableArray<DieValue> (a SmartEnum)
    // does not serialize. ImmutableArray<T> and the int-keyed ScoreTable round-trip cleanly
    // under STJ. Needs the Ardalis.SmartEnum.SystemTextJson (8.1.0) package in Farkle.
    opts.UseSystemTextJsonForSerialization(configure: o =>
        o.Converters.Add(new SmartEnumValueConverter<DieValue, int>()));
}).IntegrateWithWolverine();
services.AddWolverine(opts => opts.Policies.AutoApplyTransactions());
```

### Error events: append AND return HTTP 400 (verified by spike)

Validation failures (`IErrorEvent`) are **appended** as stored facts and still surface as HTTP 400 —
verified end-to-end against local Postgres. A command handler returns a **tuple**
`(Result<TResponse>, Events)`: Wolverine returns the `Result` to `IMessageBus.InvokeAsync<Result<TResponse>>`
(the endpoint maps `Result.Error` → 400) **and** commits the `Events` (including the error event) in
the same transaction — no exception, so the append is not rolled back. On replay the error event is
inert (see gotcha #4 / FACT 1: Marten ignores event types with no `Apply` overload).

```csharp
[AggregateHandler]
public static (Result<RollDiceResponse>, Events) Handle(Command.RollDice cmd, GameState state, IRandom random)
{
    var roll   = Dice.FromNewRoll(random, state.DiceToRoll);
    var events = RollDiceDecider.Decide(cmd, state, roll);   // #301 decider, pure, kept as-is
    var wrapped = new Events();
    wrapped.AddRange(events);
    var error = events.OfType<IErrorEvent>().FirstOrDefault();
    return error is not null
        ? (Result.Error(error.GetType().Name), wrapped)                    // appended + HTTP 400
        : (Result.Success(Map(state.Apply(events))), wrapped);            // appended + HTTP 200
}
```

> The #301 deciders are kept as pure helper functions that the thin `[AggregateHandler]` calls,
> rather than inlined — this preserves the existing decider unit-test suite unchanged while the
> handler remains the Wolverine/decider entry point.

### Handler identity: convention now (Option A), Wolverine.HTTP as the #303 target (Option C)

`[AggregateHandler]` resolves the stream id **by convention** — a `{Aggregate}Id` or `Id` property on
the command, of the stream-identity type (string). There is **no `[Identity]` attribute** in Wolverine
6.16 (that is a later release). Because our stream key is the derived string `"game-{code}"` while the
command carries the `int` game code, three options exist; all were checked against local Postgres.

**Option A — computed `Id` string on the command (chosen for #302; verified).** Keep `int GameId` as
the API identity and add a one-line computed property so the convention resolves the stream, letting
`[AggregateHandler]` do the `FetchForWriting` + append + optimistic-concurrency + save:

```csharp
public record RollDice(int GameId, int PlayerId)
{
    public string Id => $"game-{GameId}";        // matches the [AggregateHandler] convention
}

[AggregateHandler]
public static (Result<RollResult>, Events) Handle(Command.RollDice cmd, GameState game, IRandom rng)
{ /* roll → RollDiceDecider.Decide → (Result, Events) */ }
```

Verified: the stream resolves from the computed `Id` on the `IMessageBus.InvokeAsync` path, the `Events`
(including appended error events) commit, and the `Result` flows back to the caller. Cost: a one-line
computed `Id` per command (a mild leak of the `"game-{code}"` format onto the command). This is a
temporary bridge, not throwaway — see Option C.

**Option B — explicit `FetchForWriting` (also verified).** No attribute; the handler calls
`session.Events.FetchForWriting<GameState>($"game-{cmd.GameId}")` itself. Keeps commands `int`-only at
the cost of ~2 lines per handler. Equivalent behaviour; A is preferred for being the idiomatic
`[AggregateHandler]` shape with less boilerplate.

**Option C — Wolverine.HTTP `[WriteAggregate]` route binding + strong-typed id (the #303 target).** When
endpoints collapse into Wolverine.HTTP (#303), the endpoint *is* the handler and the aggregate is sourced
from the route argument, erasing the FastEndpoint + `IMessageBus.InvokeAsync` + mapper layer entirely:

```csharp
[WolverinePost("/api/games/{gameId}/players/{playerId}/rolls")]
public static (RollDiceResponse, Events) Post(
    GameId gameId, PlayerId playerId,               // strong-typed, bound from the route (Wolverine 5+)
    [WriteAggregate] GameState game, IRandom rng) { /* … */ }
```

C is **better** — one method per slice with zero glue, type-safe identity end-to-end (no computed `Id`),
and the framework owns route→stream binding, `FetchForWriting`, the concurrency `VersionSource`, and
404/ProblemDetails. It is deliberately **out of scope for #302**, which keeps FastEndpoints +
`IMessageBus.InvokeAsync` and mandates *no `swagger.json` change*; adopting Wolverine.HTTP + a strong-typed
`GameId` now would rewrite the HTTP surface, force a Kiota/swagger regen, and ripple through routes, DTOs,
the client, and tests. So **Option A ships in #302 and evolves into Option C at #303**: `[WriteAggregate("gameId")]`
route-binding replaces the computed `Id`, the handlers become endpoints, and the decider / `Events` /
`GameState`-aggregate core carries over unchanged. (A Marten *natural key* — 8.23+/Wolverine 5.18 — is the
candidate at #303 for keeping the int game code first-class alongside a surrogate stream id.)

## Consequences

- **Simpler, grain-following code**: the wrapper, the manual `Evolve`, the standalone
  `Decide` types, and one whole project all disappear. Fewer moving parts than ADR 0003.
- **`GameState` and the events lose their framework-agnostic status.** `GameState` now
  references Marten (for the id/convention shape) and the aggregation analyzer runs over it.
  Command records + the value objects in their signatures become `public` (the write-side's
  message contract), as in ADR 0003; events become `public` too (Marten discovers the
  `Apply` overloads). Never modify a V1 event schema — upcast to V2 at registration.
- **The purity guardrail is gone by choice.** The domain rules are still isolated *logically*
  (pure static handlers, no I/O in the decision) and covered by the same decider-style unit
  tests, but there is no longer an assembly/arch-test wall. This is the documented Critter
  Stack trade-off: less ceremony, direct framework use, testability preserved at the function
  level rather than the project level.
- **The five ADR 0003 gotchas still apply** to the registration (runtime compiler package in
  dev, `AutoApplyTransactions`, string identity on a clean schema, native id) — but gotcha #4
  (source generator only runs where the analyzer is referenced) is now *resolved* rather than
  *worked around*, because the analyzer references `Farkle`.

## Phase A implementation checklist (supersedes ADR 0003's)

1. `Farkle.csproj`: add `Marten`, `WolverineFx.Marten`, `WolverineFx.RuntimeCompilation`;
   remove the `Farkle.Features` project from the solution and fold its file in.
2. `GameState`: drop the Eventuous `State<>` base + the `On<>` ctor; make it `public record` with
   public-init properties (STJ rehydration); add the `string Id`, `int Code`, `Create`, and `Apply`
   conventions. Keep the pure `Fold` (decider/handler unit tests arrange state through it).
3. Make `Command.*`, `GameId`, `PlayerId`, `DieValue`, `Player`, `Score`, and the event records `public`;
   add a computed `Id => $"game-{GameId}"` to each stream-mutating command (Option A identity).
4. One thin `[AggregateHandler]` handler per stream-mutating slice (JoinPlayer, BeginGame, RollDice,
   KeepDice, SetDiceAside, ReturnDice, PassTurn) that calls its kept #301 decider and returns
   `(Result, Events)`; `StartGame` is a plain `StartStream` handler.
5. `AddFarkleCritterStack` in `Farkle`: Marten (`StreamIdentity.AsString`,
   `Snapshot<GameState>(Inline)`, `UseSystemTextJsonForSerialization` + `SmartEnumValueConverter<DieValue,int>`)
   + `IntegrateWithWolverine` + Wolverine `AutoApplyTransactions`.
6. Endpoints call `IMessageBus.InvokeAsync<Result<TResponse>>`; `IErrorEvent` still maps to HTTP 400.
7. Drop the framework-free arch-tests; keep slice-cohesion ones.
8. A Marten integration test (local Postgres, `farkle_marten`) driving a full game through the
   handlers; domain unit tests call the static handlers directly.

## Implementation notes (deltas found during the cutover, #302 / PR #309)

These refine the checklist above with what the actual cutover required:

- **Response mappers moved into `Farkle.Features`.** `LobbyMapper` / `GameStateMapper` /
  `PassTurnMapper` (GameState → Contracts DTO) used to sit in `Farkle.Application`. The slice
  `[AggregateHandler]`s now map to their response inside the slice, so the mappers were moved to
  the `Farkle.Features` namespace (`src/Farkle/Features/Responses/`). This keeps
  `KeepSlicesOffTheApplicationAndInfrastructureLayers` satisfied (a slice must not reach into the
  application layer). `GameNotifier` (application) and the read endpoint reference them the other
  way, which no guardrail forbids.
- **Architecture guardrails updated, not just dropped.** `KeepDomainTypesInternal` is removed — the
  event-sourcing contract (events, `GameState`, commands, value objects) is now *public* because it
  is the persisted Marten/STJ serialization contract (the standard Critter Stack convention). Purity
  is still enforced by `DomainPurityShould.NotDependOnApplicationEndpointsWebOrInfrastructure` and the
  decider-purity test. `DependencyRulesShould` no longer forbids the core from Marten/Wolverine/Npgsql
  (that inward coupling is the point of going native); EF Core / SignalR / Identity stay out of core.
- **Post-commit broadcast, not a subscription.** Turn/table/lobby SignalR pushes are triggered by the
  endpoints via `GameNotifier` after a committed `InvokeAsync` (reads the up-to-date Inline snapshot).
  This replaces the Eventuous `$all` broadcast subscription. Moving it onto Wolverine's outbox as a
  cascading message is deferred to #305.
- **Domain-event telemetry deferred.** The Eventuous `$all` `GameTelemetryHandler` (which logged every
  committed event as an Application Insights custom event) is deleted with the rest of the subscription
  machinery. The pure `GameTelemetry.Log` shape (and its unit test) is kept so the mapping can be
  re-wired onto a Marten event subscription / Wolverine handler in the same #305 follow-up.
- **DB-free boots per path.** NSwag swagger extraction boots Marten lightweight (dummy connection,
  lazy connect, Wolverine mediator-only) — `verify-generated` produces no drift. The storyboard
  capture and the e2e happy-path both boot the real Marten + Wolverine backend on a lightweight
  Postgres Testcontainer (the storyboard is no longer in-memory / DB-free).
