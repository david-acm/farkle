# 3. Marten + Wolverine integration shape (validated by spike)

Status: **Superseded by [ADR 0004](0004-marten-native-domain.md)** (#302). Originally Accepted;
validated end-to-end against a local PostgreSQL 16 on Marten 9.12.0 / WolverineFx 6.16.0 /
.NET 10.

> The spike findings below (the five Marten 9 / Wolverine 6 gotchas, the registration shape)
> remain accurate and useful. What changed: ADR 0004 drops the framework-free constraint that
> forced the **`GameDocument` wrapper + manual `Evolve`** design recorded here. With the domain
> referencing Marten, `GameState` is the self-aggregating snapshot directly (conventional
> `Create`/`Apply`), so the wrapper and the manual projection are removed — and gotcha #4 is
> *resolved* (the analyzer now runs over `HotDice`) rather than worked around.

## Context

#302 cuts the write/read path over from Eventuous + ESDB to the Critter Stack. The domain
was reshaped into pure deciders + `GameState.Fold` in #301, and must stay **framework-free**
(`HotDice` references no Marten/Wolverine). Several Marten 9 / Wolverine 6 behaviours are not
obvious and were pinned by spike rather than guessed.

## Gotchas found (each cost a real debug cycle; all handled below)

1. **Wolverine 6 ships no runtime compiler.** Dev needs the `WolverineFx.RuntimeCompilation`
   package; production pre-generates static code (`dotnet run -- codegen write` +
   `TypeLoadMode.Static`) — deferred to #305.
2. **Handlers silently do not commit** without `opts.Policies.AutoApplyTransactions()` (unless
   an `[AggregateHandler]` is on the path). No error — just nothing persisted.
3. **`StreamIdentity.AsString` must be set on a clean schema.** Switching identity on an
   existing Guid-keyed schema fails Marten DDL. Greenfield migration avoids this.
4. **Marten 9 dispatches `Apply`/`Create` via a compile-time source generator with no runtime
   fallback, only for types defined in the assembly where the analyzer runs.** `GameState`
   lives in the Marten-free `HotDice`, so it cannot use the conventions. Use a manual `Evolve`
   override instead.
5. **A stored projection document needs a Marten-native id** (string/Guid/int). `GameState`'s
   id is the strongly-typed `GameId`, which Marten rejects.

## Decision

Keep `GameState` **pure and unchanged**. Introduce a thin Marten-side **wrapper aggregate** in
`HotDice.Features` that carries the pure state plus the string stream key, folded by a manual
`Evolve` override that delegates to `GameState.Fold`. Commands become **public** records (they
are the Wolverine messages; Wolverine does not discover internal handlers/messages). Events
stay internal — they flow through Marten as `object`.

### Registration (validated)

```csharp
services.AddMarten(opts =>
{
    opts.Connection(connectionString);
    opts.Events.StreamIdentity = StreamIdentity.AsString;          // "game-{code}"
    opts.Projections.Add(new GameProjection(), ProjectionLifecycle.Inline);
}).IntegrateWithWolverine();
services.AddWolverine(opts => opts.Policies.AutoApplyTransactions());
// + WolverineFx.RuntimeCompilation referenced (dev codegen)
```

### Wrapper aggregate + projection (in HotDice.Features, where Marten's analyzer runs)

```csharp
public record GameDocument                    // Marten doc: string id + the pure state
{
    public string Id { get; init; } = "";
    public GameState State { get; init; } = new();
}

public class GameProjection : SingleStreamProjection<GameDocument, string>
{
    public override GameDocument Evolve(GameDocument? snapshot, string id, IEvent e)
        => new() { Id = id, State = GameState.Fold(snapshot?.State ?? new GameState(), e.Data) };
}
```

### Handlers (public, in HotDice.Features) — the decider slots into FetchForWriting

```csharp
public static class StartGameHandler
{
    public static void Handle(Command.StartGame c, IDocumentSession s) =>
        s.Events.StartStream<GameDocument>($"game-{c.GameId.Id}", StartGameDecider.Decide(c, new GameState()).ToArray());
}

public static class RollDiceHandler   // representative aggregate handler
{
    public static async Task Handle(Command.RollDice c, IDocumentSession s, IRandom random)
    {
        var stream = await s.Events.FetchForWriting<GameDocument>($"game-{c.GameId.Id}");
        var state  = stream.Aggregate?.State ?? new GameState();
        var roll   = Dice.FromNewRoll(random, state.DiceToRoll);
        stream.AppendMany(RollDiceDecider.Decide(c, state, roll));
    }
}
```

Reads: `querySession.LoadAsync<GameDocument>($"game-{code}")` (or `FetchLatest`), then map
`.State` to the `GameView` DTO.

## Consequences

- `GameState` needs **no reshape** for the write path; the Eventuous base types
  (`State<>`/`GameId : Id`/`[EventType]`) are removed only at the final cleanup when Eventuous
  is deleted.
- `Command` records + the value objects in their signatures (`GameId`, `PlayerId`, `DieValue`)
  become **public**. This is idiomatic — under a message bus, commands are the write-side's
  public contract. The domain-purity guardrail (no framework deps) is unaffected by visibility.
- The manual `Evolve` projection is the seam that lets a framework-free domain drive a Marten
  projection.

## Phase A implementation checklist (next step)

1. `HotDice`: make `Command.*`, `GameId`, `PlayerId`, `DieValue` public; add
   `[InternalsVisibleTo("HotDice.Features")]` (for events/deciders/`Fold`/`GameState`).
2. `HotDice.Features`: `GameDocument` + `GameProjection`; register the projection in
   `CritterStackServiceExtensions`.
3. `HotDice.Features`: seven public handlers (StartGame, JoinPlayer, BeginGame, RollDice,
   KeepDice, SetDiceAside, ReturnDice, PassTurn) following the template above.
4. A Marten integration test (local Postgres) driving a game through the handlers.
5. Build green; Eventuous still serves the endpoints (the flip + deletion is Phase B/C).

Local Postgres for verification (ephemeral per session):
`pg_ctlcluster 16 main start` · set `postgres`/`changeit` · db `farkle_marten`.
