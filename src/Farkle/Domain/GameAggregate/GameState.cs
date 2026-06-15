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
      DiceSetAside = ImmutableArray<DieValue>.Empty,
      StraightsKeptThisTurn = new DiceAreStraight(Dice.FromValues(e.Dice)).IsSatisfied().IsValid ? state.StraightsKeptThisTurn + 1 : state.StraightsKeptThisTurn
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
      Players = e.PlayerOrder,
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
    return state with
    {
      Players = state.Players.Add(new Player(playerJoined.Id, playerJoined.Name)),
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
