using System.Collections.Immutable;
using Eventuous;
using static Farkle.Domain.GameAggregate.Command;
using static Farkle.Domain.GameAggregate.GameEvents;

namespace Farkle.Domain.GameAggregate;

internal record GameState : State<GameState>
{
  public GameState()
  {
    On<V1.GameStarted>(HandleGameStarted);
    On<V1.PlayerJoined>(HandlePlayerJoined);
    On<V1.DiceRolled>(HandleDiceRolled);
    On<V2.DiceRolled>(HandleDiceRolledV2);
    On<V1.TurnPassed>(HandleTurnPassed);
    On<V1.DiceKept>(HandleDiceKept);
    On<V2.DiceKept>(HandleDiceKeptV2);
  }

  // TODO: Remove nullable
  public GameId?   Id        { get; private init; }
  public GameStage GameStage { get; private init; }
  public Score     TurnScore { get; private init; } = new(0);

  public ImmutableArray<Player>    Players     { get; private init; } = ImmutableArray<Player>.Empty;
  public ImmutableArray<DieValue> TableCenter { get; private init; } = ImmutableArray<DieValue>.Empty;
  public ImmutableArray<DieValue> DiceKept    { get; private init; } = ImmutableArray<DieValue>.Empty;
  public int StraightsKeptThisTurn { get; private init; } = 0;

  public ImmutableDictionary<int, int> ScoreTable { get; private init; } =
    ImmutableDictionary<int, int>.Empty;

  internal int PlayerInTurn => Players.IsEmpty ? 0 : Players[0].Id;

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
      StraightsKeptThisTurn = new DiceAreStraight(Dice.FromValues(e.Dice)).IsSatisfied().IsValid ? state.StraightsKeptThisTurn + 1 : state.StraightsKeptThisTurn
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
      StraightsKeptThisTurn = new DiceAreStraight(Dice.FromValues(e.Dice)).IsSatisfied().IsValid ? state.StraightsKeptThisTurn + 1 : state.StraightsKeptThisTurn
    };
  }

  private static GameState HandleTurnPassed(GameState state, GameEvents.V1.TurnPassed e)
  {
    return state with
    {
      Players = e.PlayerOrder,
      ScoreTable = state.ScoreTable.SetItem(
        e.PlayerId,
        e.GameScore),
      TableCenter = state.TableCenter.AddRange(state.DiceKept),
      GameStage = GameStage.Rolling,
      DiceKept = state.TableCenter.Clear(),
      StraightsKeptThisTurn = 0
    };
  }

  private static GameState HandleDiceRolled(GameState state, GameEvents.V1.DiceRolled e)
  {
    return state with
    {
      TurnScore = e.TurnScore,
      TableCenter = ImmutableArray<DieValue>.Empty.AddRange(e.Dice.ToDiceValues()),
      GameStage = GameStage.Keeping
    };
  }

  private static GameState HandleDiceRolledV2(GameState state, V2.DiceRolled e)
  {
    return state with
    {
      TurnScore = e.TurnScore,
      TableCenter = ImmutableArray<DieValue>.Empty.AddRange(e.Dice.ToDiceValues()),
      GameStage = e.Stage
    };
  }

  private static GameState HandlePlayerJoined(GameState state, GameEvents.V1.PlayerJoined playerJoined)
  {
    return state with
    {
      Players = state.Players.Add(new Player(playerJoined.Id, playerJoined.Name)),
      ScoreTable = state.ScoreTable.Add(playerJoined.Id, 0)
    };
  }

  private static GameState HandleGameStarted(GameState gameState, GameEvents.V1.GameStarted e)
  {
    return gameState with { Id = e.Id, GameStage = GameStage.Rolling };
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
