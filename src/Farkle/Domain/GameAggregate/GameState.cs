using System.Collections.Immutable;
using Farkle.SharedKernel.Scoring;
using Farkle.SharedKernel.Turns;
using static Farkle.Domain.GameAggregate.GameEvents;

namespace Farkle.Domain.GameAggregate;

// GameState is the Marten self-aggregating snapshot (ADR 0004): Marten's conventional Create/Apply
// methods fold the "game-{code}" event stream into it, and it is stored as the game document — so it
// is both the write-side aggregate (via FetchForWriting) and the read model (mapped to GameView).
// Properties are public-init so Marten's STJ serializer can rehydrate the stored document.
public record GameState
{
  // Marten document id == the "game-{code}" stream key (ADR 0002). Empty on the initial state and
  // assigned by Create(GameStarted); "does this game exist?" is Code == 0 (or an empty Id).
  public string    Id        { get; init; } = "";

  // The int game code players type to join — the user-facing/API identity (the string Id derives
  // from it). 0 until a GameStarted event assigns the real code (#32's GameId.None sentinel).
  public int       Code      { get; init; }

  public GameStage GameStage { get; init; }
  public Player?   Winner    { get; init; }
  public Score     TurnScore { get; init; } = new(0);

  // #301 — whether the in-turn player has rolled or kept this turn. Replaces the old event-history
  // peek so "can I pass?" is a pure (command, state) decision: you may pass only once you've acted.
  public bool HasActedThisTurn { get; init; }

  public ImmutableArray<Player>    Players     { get; init; } = ImmutableArray<Player>.Empty;
  public ImmutableArray<DieValue> TableCenter { get; init; } = ImmutableArray<DieValue>.Empty;
  public ImmutableArray<DieValue> DiceKept    { get; init; } = ImmutableArray<DieValue>.Empty;

  // #159 — the in-turn player's transient set-aside selection: a non-destructive overlay on
  // TableCenter. Reset whenever the roll moves on (roll, keep, pass) so it never leaks across turns.
  public ImmutableArray<DieValue> DiceSetAside { get; init; } = ImmutableArray<DieValue>.Empty;
  public int StraightsKeptThisTurn { get; init; } = 0;

  // #244 — a server-assigned, replay-derived turn ordinal. 0 before play; 1 once the game starts;
  // increments on every TurnPassed. The single source of truth all players agree on.
  public int TurnNumber { get; init; } = 0;

  public ImmutableDictionary<int, int> ScoreTable { get; init; } =
    ImmutableDictionary<int, int>.Empty;

  internal int PlayerInTurn => Players.IsEmpty ? 0 : Players[0].Id;

  // How many dice the next roll throws: the six minus whatever is already kept this turn.
  internal int DiceToRoll => 6 - DiceKept.Length % 6;

  // ── Marten self-aggregation conventions (ADR 0004) ──
  // Create builds the initial document from the stream's first event; Apply folds each subsequent
  // event. Error events (IErrorEvent) deliberately have no Apply overload — they are appended as
  // stored facts but are inert on replay (Marten ignores event types with no matching method).
  public static GameState Create(V1.GameStarted e) => HandleGameStarted(new GameState(), e);

  public GameState Apply(V1.PlayerJoined e)    => HandlePlayerJoined(this, e);
  public GameState Apply(V2.PlayerJoined e)    => HandlePlayerJoinedV2(this, e);
  public GameState Apply(V1.GamePlayStarted e) => HandleGamePlayStarted(this, e);
  public GameState Apply(V1.DiceRolled e)      => HandleDiceRolled(this, e);
  public GameState Apply(V2.DiceRolled e)      => HandleDiceRolledV2(this, e);
  public GameState Apply(V1.TurnPassed e)      => HandleTurnPassed(this, e);
  public GameState Apply(V1.DiceKept e)        => HandleDiceKept(this, e);
  public GameState Apply(V2.DiceKept e)        => HandleDiceKeptV2(this, e);
  public GameState Apply(V1.DiceSetAside e)    => HandleDiceSetAside(this, e);
  public GameState Apply(V1.DiceReturned e)    => HandleDiceReturned(this, e);
  public GameState Apply(V1.GameWon e)         => HandleGameWon(this, e);

  // Pure event fold — the framework-free way to rebuild state, used by decider/handler unit tests to
  // arrange a state. Delegates to the same static Handle* methods as the Marten Apply overloads;
  // error events and unknowns are no-ops (they never mutate state).
  internal static GameState Fold(GameState state, object @event) => @event switch
  {
    V1.GameStarted e     => HandleGameStarted(state, e),
    V1.PlayerJoined e    => HandlePlayerJoined(state, e),
    V2.PlayerJoined e    => HandlePlayerJoinedV2(state, e),
    V1.GamePlayStarted e => HandleGamePlayStarted(state, e),
    V1.DiceRolled e      => HandleDiceRolled(state, e),
    V2.DiceRolled e      => HandleDiceRolledV2(state, e),
    V1.TurnPassed e      => HandleTurnPassed(state, e),
    V1.DiceKept e        => HandleDiceKept(state, e),
    V2.DiceKept e        => HandleDiceKeptV2(state, e),
    V1.DiceSetAside e    => HandleDiceSetAside(state, e),
    V1.DiceReturned e    => HandleDiceReturned(state, e),
    V1.GameWon e         => HandleGameWon(state, e),
    _                    => state
  };

  internal static GameState Fold(GameState state, IEnumerable<object> events) =>
    events.Aggregate(state, Fold);

  internal static GameState Fold(params object[] events) =>
    Fold(new GameState(), events);

  public Score GameScoreFor(PlayerId playerId)
  {
    return new Score(ScoreTable.GetValueOrDefault(playerId, 0));
  }

  public Player GetPlayer(int id)
  {
    return Players.Single(p => p.Id == id);
  }

  private static GameState HandleDiceKept(GameState state, GameEvents.V1.DiceKept e)
  {
    return state with
    {
      DiceKept = state.DiceKept.AddRange(Dice.FromValues(e.Dice).DiceValues),
      TurnScore = e.NewTurnScore,
      TableCenter = ImmutableArray<DieValue>.Empty.AddRange(e.TableCenter.ToDiceValues()),
      GameStage = GameStage.Rolling,
      DiceSetAside = ImmutableArray<DieValue>.Empty,
      HasActedThisTurn = true,
      StraightsKeptThisTurn = ScoreCalculator.Evaluate(e.Dice).Tricks.Contains(ScoringTrick.FourOfAKind) ? state.StraightsKeptThisTurn + 1 : state.StraightsKeptThisTurn
    };
  }

  private static GameState HandleDiceKeptV2(GameState state, V2.DiceKept e)
  {
    return state with
    {
      DiceKept = state.DiceKept.AddRange(Dice.FromValues(e.Dice).DiceValues),
      TurnScore = e.NewTurnScore,
      TableCenter = ImmutableArray<DieValue>.Empty.AddRange(e.TableCenter.ToDiceValues()),
      GameStage = e.Stage,
      DiceSetAside = ImmutableArray<DieValue>.Empty,
      HasActedThisTurn = true,
      StraightsKeptThisTurn = ScoreCalculator.Evaluate(e.Dice).Tricks.Contains(ScoringTrick.FourOfAKind) ? state.StraightsKeptThisTurn + 1 : state.StraightsKeptThisTurn
    };
  }

  private static GameState HandleDiceSetAside(GameState state, V1.DiceSetAside e)
  {
    return state with { DiceSetAside = state.DiceSetAside.Add(DieValue.FromValue(e.Die)) };
  }

  private static GameState HandleDiceReturned(GameState state, V1.DiceReturned e)
  {
    return state with { DiceSetAside = state.DiceSetAside.Remove(DieValue.FromValue(e.Die)) };
  }

  private static GameState HandleTurnPassed(GameState state, GameEvents.V1.TurnPassed e)
  {
    return state with
    {
      // Re-derive each player's colour from its id so the rotated order always carries a colour.
      Players = e.PlayerOrder
        .Select(p => p with { Color = PlayerColors.For(p.Id) })
        .ToImmutableArray(),
      ScoreTable = state.ScoreTable.SetItem(
        e.PlayerId,
        e.GameScore),
      TableCenter = state.TableCenter.AddRange(state.DiceKept),
      GameStage = GameStage.Rolling,
      DiceKept = state.TableCenter.Clear(),
      DiceSetAside = ImmutableArray<DieValue>.Empty,
      StraightsKeptThisTurn = 0,
      HasActedThisTurn = false, // next player hasn't acted yet
      TurnScore = new Score(0),
      TurnNumber = state.TurnNumber + 1 // #244 — a pass advances to the next turn
    };
  }

  private static GameState HandleDiceRolled(GameState state, GameEvents.V1.DiceRolled e)
  {
    return state with
    {
      TurnScore = e.TurnScore,
      TableCenter = ImmutableArray<DieValue>.Empty.AddRange(e.Dice.ToDiceValues()),
      DiceSetAside = ImmutableArray<DieValue>.Empty,
      HasActedThisTurn = true,
      GameStage = GameStage.Keeping
    };
  }

  private static GameState HandleDiceRolledV2(GameState state, V2.DiceRolled e)
  {
    return state with
    {
      TurnScore = e.TurnScore,
      TableCenter = ImmutableArray<DieValue>.Empty.AddRange(e.Dice.ToDiceValues()),
      DiceSetAside = ImmutableArray<DieValue>.Empty,
      HasActedThisTurn = true,
      GameStage = e.Stage
    };
  }

  private static GameState HandlePlayerJoined(GameState state, GameEvents.V1.PlayerJoined playerJoined)
  {
    // V1 events predate player colours — derive the colour from the id so old streams still render.
    return state with
    {
      Players = state.Players.Add(
        new Player(playerJoined.Id, playerJoined.Name, PlayerColors.For(playerJoined.Id))),
      ScoreTable = state.ScoreTable.Add(playerJoined.Id, 0)
    };
  }

  private static GameState HandlePlayerJoinedV2(GameState state, V2.PlayerJoined playerJoined)
  {
    return state with
    {
      Players = state.Players.Add(
        new Player(playerJoined.Id, playerJoined.Name, playerJoined.Color)),
      ScoreTable = state.ScoreTable.Add(playerJoined.Id, 0)
    };
  }

  private static GameState HandleGameStarted(GameState gameState, GameEvents.V1.GameStarted e)
  {
    return gameState with { Id = $"game-{e.Id}", Code = e.Id, GameStage = GameStage.WaitingForPlayers };
  }

  private static GameState HandleGamePlayStarted(GameState state, GameEvents.V1.GamePlayStarted e)
  {
    return state with { GameStage = GameStage.Rolling, TurnNumber = 1 }; // #244 — first turn
  }

  private static GameState HandleGameWon(GameState state, GameEvents.V1.GameWon e)
  {
    return state with
    {
      GameStage = GameStage.Finished,
      Winner = state.Players.Single(p => p.Id == e.PlayerId)
    };
  }
}

public record Score(int Value)
{
  public static implicit operator int(Score score)
  {
    return score.Value;
  }

  public static implicit operator Score(int score)
  {
    return new Score(score);
  }
}

// GameId is a plain int wrapper (the user-facing game code) now that Eventuous' Id base type is gone
// (ADR 0004). Commands still carry it; the Marten stream/document key derives from it as "game-{Id}".
public record GameId(int Id)
{
  // Sentinel for "no game yet". Real game ids are generated in [100_000, 1_000_000)
  // (RandomGameIdGenerator), so 0 never collides with a live game. (#32)
  public static readonly GameId None = new(0);

  public static implicit operator GameId(int id)
  {
    return new GameId(id);
  }

  public static implicit operator int(GameId id)
  {
    return id.Id;
  }
}

// Color is the player's identity colour (a hex string), assigned by join order from PlayerColors.
// Always derived from the player's id, so it is stable across replays and snapshot round-trips.
// (Moved from the deleted Eventuous Game aggregate.)
public record Player(int Id, string Name, string Color = "");
