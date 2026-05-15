using System.Collections.Immutable;
using Eventuous;
using V1 = Farkle.Domain.GameAggregate.GameEvents.V1;
using V2 = Farkle.Domain.GameAggregate.GameEvents.V2;

namespace Farkle.Domain.GameAggregate;

internal class Game : Aggregate<GameState>
{
  private readonly IRandom _randomProvider;

  public Game() : this(default!)
  {
  }

  public Game(IRandom? randomProvider)
  {
    _randomProvider = randomProvider ?? new DefaultRandomProvider();
  }

  public void Start(Command.StartGame startGame)
  {
    Apply(new GameEvents.V1.GameStarted(startGame));
  }

  public void JoinPlayer(Command.JoinPlayer joinPlayer)
  {
    Apply(new GameEvents.V1.PlayerJoined(joinPlayer.Id, joinPlayer.Name));
  }

  public void RollDiceV1(Command.RollDice rollDice)
  {
    var roll = Dice.FromNewRoll(
      _randomProvider,
      GetNumberOfDiceToTrow());

    Apply(new GameEvents.V1.DiceRolled(
      rollDice.PlayerId,
      roll.DiceValues.ToPrimitiveArray(),
      GetScoreAfterRoll(roll)));
  }

  public void RollDiceV2(Command.RollDice rollDice)
  {
    var roll = Dice.FromNewRoll(
      _randomProvider,
      GetNumberOfDiceToTrow());

    Apply(new V2.DiceRolled(
      rollDice.PlayerId,
      roll.DiceValues.ToPrimitiveArray(),
      GetScoreAfterRoll(roll),
      GameStage.Keeping));
  }

  public void PassTurn(Command.PassTurn passTurn)
  {
    var newScore = GetScore(passTurn.PlayerId);
    Apply(new GameEvents.V1.TurnPassed(
      passTurn.PlayerId,
      GetPlayerOrder(passTurn.PlayerId),
      newScore));

    if (newScore >= 10_000)
      base.Apply(new GameEvents.V1.GameWon(passTurn.PlayerId, newScore));
  }

  public void KeepDice(Command.KeepDice keepDice)
  {
    Apply(new V1.DiceKept(keepDice.PlayerId, keepDice.DiceValues.ToPrimitiveArray(),
      GetTableCenterDice(keepDice),
      GetNewTurnScore(keepDice.DiceValues, State.TurnScore, State)));
  }

  public void KeepDiceV2(Command.KeepDice keepDice)
  {
    Apply(new V2.DiceKept(keepDice.PlayerId, keepDice.DiceValues.ToPrimitiveArray(),
      GetTableCenterDice(keepDice),
      GetNewTurnScore(keepDice.DiceValues, State.TurnScore, State),
      GameStage.Rolling));
  }

  private int[] GetTableCenterDice(Command.KeepDice keepDice)
  {
    var tableCenter = State.TableCenter.RemoveRange(keepDice.DiceValues);
    if (!tableCenter.IsEmpty)
    {
      return tableCenter.ToPrimitiveArray();
    }

    return State.TableCenter.AddRange(keepDice.DiceValues).ToPrimitiveArray();
  }

  private int GetScore(Command.PlayerId playerId)
  {
    return State.GameScoreFor(playerId) + State.TurnScore;
  }

  private ImmutableArray<Player> GetPlayerOrder(int playerId)
  {
    var player        = State.GetPlayer(playerId);
    var newPlayerList = State.Players.Remove(player).Add(player);
    return newPlayerList;
  }

  private void Apply(object @event)
  {
    // TODO: Change Exception for Ardalis.Result
    var result = GameValidator.ValidatePreconditions(this, @event);

    if (result.IsValid)
    {
      base.Apply(@event);
      return;
    }

    base.Apply(result.FailedValidationEvent);
  }

  private int GetNumberOfDiceToTrow()
  {
    var kept = State.DiceKept.Length % 6;
    return 6 - kept;
  }

  private static int GetNewTurnScore(IEnumerable<DieValue> diceKept, int currentScore, GameState state)
  {
    var dice = new Dice(diceKept);
    var tricks = new Dictionary<Validator, int>
    {
      {
        new DiceAreStraight(dice), 1000
      },
      {
        new DiceAreTrips(dice), (dice.DiceValues.FirstOrDefault()?.Value ?? 0) * 100
      },
      {
        new DiceAreOnesOrFives(dice), (dice.DiceValues.Count(d => d == DieValue.One)  * 100) +
                                      (dice.DiceValues.Count(d => d == DieValue.Five) * 50)
      },
      {
        new DiceAreStair(dice), 1500
      }
    };

    var turnScore = tricks.FirstOrDefault(v => v.Key.IsSatisfied()).Value;
    
    var isStraight = new DiceAreStraight(dice).IsSatisfied().IsValid;
    if (isStraight && state.StraightsKeptThisTurn > 0)
    {
        return new Score((currentScore + turnScore) * 2);
    }

    return new Score(currentScore + turnScore);
  }

  private Score GetScoreAfterRoll(Dice dice)
  {
    var canKeepAnyDice = new CanKeepDice(dice).IsSatisfied();

    return canKeepAnyDice ? State.TurnScore : new Score(0);
  }
}

internal record Player(int Id, string Name);

internal enum GameStage
{
  None,
  Rolling,
  Keeping,
  Finished
}
