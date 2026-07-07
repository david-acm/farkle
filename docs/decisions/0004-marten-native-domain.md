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
2. **Commands are handled by pure static `[AggregateHandler]` methods** — one per slice,
   `Handle(Command.X cmd, GameState state, <deps>) → events`. This method *is* the decider:
   the #301 decider bodies (including validation-as-events returning `IErrorEvent`) move into
   it near-verbatim, and it stays directly unit-testable as a pure `(command, state) → events`
   function. `RollDice` uses a compound handler (`Before`/`Load`) to roll `IRandom` and pass
   the dice into the pure decision.
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
}).IntegrateWithWolverine();
services.AddWolverine(opts => opts.Policies.AutoApplyTransactions());
```

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
2. `GameState`: drop any Eventuous base type; add the `string Id`, `Create`, and `Apply`
   conventions; delete `Fold` once nothing calls it.
3. Make `Command.*`, `GameId`, `PlayerId`, `DieValue`, and the event records `public`.
4. One `[AggregateHandler]` static handler per slice (StartGame, JoinPlayer, BeginGame,
   RollDice, KeepDice, SetDiceAside, ReturnDice, PassTurn), each carrying its #301 decider body.
5. `AddFarkleCritterStack` in `Farkle`: Marten (`StreamIdentity.AsString`,
   `Snapshot<GameState>(Inline)`) + `IntegrateWithWolverine` + Wolverine `AutoApplyTransactions`.
6. Endpoints call `IMessageBus.InvokeAsync`; `IErrorEvent` still maps to HTTP 400.
7. Drop the framework-free arch-tests; keep slice-cohesion ones.
8. A Marten integration test (local Postgres, `farkle_marten`) driving a full game through the
   handlers; domain unit tests call the static handlers directly.
