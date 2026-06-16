using System.Collections.Immutable;
using Eventuous;
using Farkle.SharedKernel.Scoring;
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

  // TODO: Remove nullable
  public GameId?   Id        { get; private init; }
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

  public ImmutableDictionary<int, int> ScoreTable { get; private init; } =
    ImmutableDictionary<int, int>.Empty;

  internal int PlayerInTurn => Players.IsEmpty ? 0 : Players[0].Id;

  // #156 — the single sanctioned seam for rebuilding a state from a persisted read-model
  // snapshot, so the incremental projector can fold the next event onto it via When(). Keeps
  // the init-properties private (the aggregate is still the only thing that mutates state
  // through events) while letting the read-side serializer reconstruct a value object.
  internal static GameState FromSnapshot(
    GameId?                          id,
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
      StraightsKeptThisTurn = ScoreCalculator.Evaluate(e.Dice).Trick == ScoringTrick.FourOfAKind ? state.StraightsKeptThisTurn + 1 : state.StraightsKeptThisTurn
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
      StraightsKeptThisTurn = ScoreCalculator.Evaluate(e.Dice).Trick == ScoringTrick.FourOfAKind ? state.StraightsKeptThisTurn + 1 : state.StraightsKeptThisTurn
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
      TurnScore = new Score(0)
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
    return state with { GameStage = GameStage.Rolling };
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
  public static implicit operator GameId(int id)
  {
    return new GameId(id);
  }

  public static implicit operator int(GameId id)
  {
    return id.Id;
  }
}
