using System.Collections.Immutable;
using Eventuous;
using Farkle.SharedKernel.Scoring;
using Farkle.SharedKernel.Turns;
using static Farkle.Domain.GameAggregate.Command;
using static Farkle.Domain.GameAggregate.GameEvents;

namespace Farkle.Domain.GameAggregate;

internal record GameState : State<GameState>
{
  public GameState()
  {
    On<V1.GameStarted>(HandleGameStarted);
    On<V1.PlayerJoined>(HandlePlayerJoined);
    On<V2.PlayerJoined>(HandlePlayerJoinedV2);
    On<V1.GamePlayStarted>(HandleGamePlayStarted);
    On<V1.DiceRolled>(HandleDiceRolled);
    On<V2.DiceRolled>(HandleDiceRolledV2);
    On<V1.TurnPassed>(HandleTurnPassed);
    On<V1.DiceKept>(HandleDiceKept);
    On<V2.DiceKept>(HandleDiceKeptV2);
    On<V1.DiceSetAside>(HandleDiceSetAside);
    On<V1.DiceReturned>(HandleDiceReturned);
    On<V1.GameWon>(HandleGameWon);
  }

  // Non-nullable (#32): the empty initial state carries the GameId.None sentinel until a
  // GameStarted event assigns the real id. "Does this game exist?" is GameId.None vs a real id.
  public GameId    Id        { get; private init; } = GameId.None;
  public GameStage GameStage { get; private init; }
  public Player?   Winner    { get; private init; }
  public Score     TurnScore { get; private init; } = new(0);

  public ImmutableArray<Player>    Players     { get; private init; } = ImmutableArray<Player>.Empty;
  public ImmutableArray<DieValue> TableCenter { get; private init; } = ImmutableArray<DieValue>.Empty;
  public ImmutableArray<DieValue> DiceKept    { get; private init; } = ImmutableArray<DieValue>.Empty;

  // #159 — the in-turn player's transient set-aside selection: a non-destructive overlay
  // on TableCenter (the dice stay on the table). Reset whenever the roll moves on (roll,
  // keep, pass) so it never leaks across rolls or turns.
  public ImmutableArray<DieValue> DiceSetAside { get; private init; } = ImmutableArray<DieValue>.Empty;
  public int StraightsKeptThisTurn { get; private init; } = 0;

  // #244 — a server-assigned, replay-derived turn ordinal. 0 before play begins; 1 once the game
  // starts; increments on every TurnPassed. The single source of truth all players agree on, used
  // as a telemetry entity key so a turn (and a game) can be reconstructed across players. Derived
  // purely from events, so no event-schema change is needed.
  public int TurnNumber { get; private init; } = 0;

  public ImmutableDictionary<int, int> ScoreTable { get; private init; } =
    ImmutableDictionary<int, int>.Empty;

  internal int PlayerInTurn => Players.IsEmpty ? 0 : Players[0].Id;

  // How many dice the next roll throws: the six minus whatever is already kept this turn (a fresh
  // turn rolls all six; kept dice wrap at six). Pure, so the roll side effect (rolling that many)
  // lives in the handler while the count stays domain logic the decider and handler share.
  internal int DiceToRoll => 6 - DiceKept.Length % 6;

  // #156 — the single sanctioned seam for rebuilding a state from a persisted read-model
  // snapshot, so the incremental projector can fold the next event onto it via When(). Keeps
  // the init-properties private (the aggregate is still the only thing that mutates state
  // through events) while letting the read-side serializer reconstruct a value object.
  internal static GameState FromSnapshot(
    GameId                           id,
    GameStage                        gameStage,
    Player?                          winner,
    Score                            turnScore,
    ImmutableArray<Player>           players,
    ImmutableArray<DieValue>         tableCenter,
    ImmutableArray<DieValue>         diceKept,
    ImmutableArray<DieValue>         diceSetAside,
    int                              straightsKeptThisTurn,
    ImmutableDictionary<int, int>    scoreTable) =>
    new()
    {
      Id                    = id,
      GameStage             = gameStage,
      Winner                = winner,
      TurnScore             = turnScore,
      Players               = players,
      TableCenter           = tableCenter,
      DiceKept              = diceKept,
      DiceSetAside          = diceSetAside,
      StraightsKeptThisTurn = straightsKeptThisTurn,
      ScoreTable            = scoreTable
    };

  // Pure event fold — the framework-free way to rebuild state (used by decider tests to arrange
  // a state, and the Marten SingleStreamProjection<GameView> in #302). Reuses the same static
  // Handle* methods the Eventuous On<> registrations point at; error events and unknowns are
  // no-ops (they never mutate state). The On<> registrations above disappear at the #302 cutover,
  // leaving this as the single fold.
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
      // Re-derive each player's colour from its id so the rotated order always carries a
      // colour, even for TurnPassed events serialized before colours existed.
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
      GameStage = e.Stage
    };
  }

  private static GameState HandlePlayerJoined(GameState state, GameEvents.V1.PlayerJoined playerJoined)
  {
    // V1 events predate player colours — derive the colour from the id so old streams still
    // render the same deterministic palette colour as a freshly joined player would.
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
    return gameState with { Id = e.Id, GameStage = GameStage.WaitingForPlayers };
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

internal record Score(int Value)
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

internal record GameId(int Id) : Id($"{Id}")
{
  // Sentinel for "no game yet" — the empty GameState's default before a GameStarted event.
  // Real game ids are generated in [100_000, 1_000_000) (RandomGameIdGenerator), so 0 never
  // collides with a live game. (#32 — replaces the former nullable GameState.Id.)
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
