using Eventuous;
using static Farkle.Domain.GameAggregate.GameEvents.V1;
using static Farkle.Domain.GameAggregate.GameEvents.V2;

namespace Farkle.Domain.GameAggregate;

internal static class GameValidator
{
  public static ValidationResult ValidatePreconditions(Game game, object @event)
  {
    var state = game.State;
    var valid = @event switch
    {
      GameStarted e => Validate(
        state.GameStage == GameStage.None,
        new GameAlreadyStarted(e.Id)),
      PlayerJoined => Validate(
        state.GameStage == GameStage.Rolling,
        new GameHasNotStarted(state.GameStage)),
      GameEvents.V1.DiceRolled e =>
        new PlayerIsInTurn(state, e.PlayerId).And(new SingleRoll(state, e.PlayerId)).IsSatisfied(),
      GameEvents.V2.DiceRolled e =>
        new PlayerIsInTurn(state, e.PlayerId).And(new SingleRoll(state, e.PlayerId)).IsSatisfied(),
      TurnPassed e =>
        new PlayerIsInTurn(state, e.PlayerId).And(new PlayerCanPass(game, e.PlayerId)).IsSatisfied(),
      GameEvents.V1.DiceKept e =>
        new PlayerIsInTurn(state, e.PlayerId).And(new PlayerHasThoseDice(GetDice(e), state))
          .And(new CanKeepDice(GetDice(e)))
          .IsSatisfied(),
      GameEvents.V2.DiceKept e =>
        new PlayerIsInTurn(state, e.PlayerId).And(new PlayerHasThoseDice(GetDice(e), state))
          .And(new CanKeepDice(GetDice(e)))
          .IsSatisfied(),

      _ => Validate(false, $"No validation performed for event {@event}")
    };

    return valid;
  }

  private static Dice GetDice(GameEvents.V2.DiceKept e)
  {
    return Dice.FromValues(e.Dice.ToList());
  }


  private static Dice GetDice(GameEvents.V1.DiceKept e)
  {
    return Dice.FromValues(e.Dice.ToList());
  }

  private static ValidationResult Validate(bool validation, object failedValidationEvent)
  {
    return new ValidationResult(validation, failedValidationEvent);
  }
}

internal class SingleRoll : Validator
{
  private readonly int       _playerId;
  private readonly GameState _state;

  public SingleRoll(GameState state, int playerId)
  {
    _state    = state;
    _playerId = playerId;
  }

  public override ValidationResult IsSatisfied()
  {
    return new ValidationResult(_state.GameStage == GameStage.Rolling,
      new RolledTwice(_playerId));
  }
}

internal class PlayerHasThoseDice : Validator
{
  private readonly Dice      _dice;
  private readonly GameState _state;

  public PlayerHasThoseDice(Dice dice, GameState state)
  {
    _dice  = dice;
    _state = state;
  }

  public override ValidationResult IsSatisfied()
  {
    var tableCenter = _state.TableCenter.ToList();
    var unavailable = _dice.DiceValues.Where(d => !tableCenter.Remove(d)).ToList();
    return new ValidationResult(
      !unavailable.Any(),
      new DiceNotAllowedToBeKept(
        $"Player Does not have die/dice: {string.Join(',', unavailable)}. Dice found: {string.Join(", ", _state.TableCenter)}",
        unavailable.ToPrimitiveArray()));
  }
}

internal class DiceAreStair : Validator
{
  private readonly IEnumerable<DiceValue> _dice;

  public DiceAreStair(Dice dice)
  {
    _dice = dice.DiceValues;
  }

  public override ValidationResult IsSatisfied()
  {
    return new ValidationResult(_dice.Count() == 6              &&
                                _dice.Contains(DiceValue.One)   &&
                                _dice.Contains(DiceValue.Two)   &&
                                _dice.Contains(DiceValue.Three) &&
                                _dice.Contains(DiceValue.Four)  &&
                                _dice.Contains(DiceValue.Five)  &&
                                _dice.Contains(DiceValue.Six),
      new DiceNotAllowedToBeKept("Dice are not a stair", _dice.ToPrimitiveArray())
    );
  }
}

[EventType("V1.GameAlreadyStarted")]
internal record GameAlreadyStarted(int Id);

[EventType("V1.GameHasNotStarted")]
internal record GameHasNotStarted(GameStage GameStage);

[EventType("V1.DiceNotAllowedToBeKept")]
internal record DiceNotAllowedToBeKept(string Reason, IEnumerable<int> Dice);

internal class DiceAreOnesOrFives : Validator
{
  private readonly IEnumerable<DiceValue> _dice;

  public DiceAreOnesOrFives(Dice dice)
  {
    _dice = dice.DiceValues;
  }

  public override ValidationResult IsSatisfied()
  {
    return new ValidationResult(_dice.All(d => d == DiceValue.One || d == DiceValue.Five),
      new DiceNotAllowedToBeKept("Dice are not ones or fives", _dice.ToPrimitiveArray()));
  }
}

internal class CanKeepDice : Validator
{
  private readonly IEnumerable<DiceValue> _dice;

  public CanKeepDice(Dice dice)
  {
    _dice = dice.DiceValues;
  }

  public override ValidationResult IsSatisfied()
  {
    var diceContainOnesOrFives          = _dice.Any(d => d == DiceValue.One || d == DiceValue.Five);
    var thereAreThreeOrMoreRepeatedDice = _dice.GroupBy(d => d).MaxBy(d => d.Count())?.Count() >= 3;

    return new ValidationResult(
      diceContainOnesOrFives || thereAreThreeOrMoreRepeatedDice,
      new DiceNotAllowedToBeKept("Dice are not ones or fives",
        _dice.ToPrimitiveArray()));
  }
}

internal class DiceAreTrips : Validator
{
  private readonly IEnumerable<DiceValue> _dice;

  public DiceAreTrips(Dice dice)
  {
    _dice = dice.DiceValues;
  }

  public override ValidationResult IsSatisfied()
  {
    return new ValidationResult(AreThree(_dice) && AllDiceHaveTheSameValue(_dice), $"The dice {_dice} are not trips.");
  }

  private static bool AreThree(IEnumerable<DiceValue> destination)
  {
    return destination.Count() == 3;
  }

  private static bool AllDiceHaveTheSameValue(IEnumerable<DiceValue> destination)
  {
    return destination.GroupBy(v => v).Count() == 1;
  }
}

internal class DiceAreStraight : Validator
{
  private readonly IEnumerable<DiceValue> _dice;

  public DiceAreStraight(Dice dice)
  {
    _dice = dice.DiceValues;
  }

  public override ValidationResult IsSatisfied()
  {
    return new ValidationResult(_dice.Count() == 4 && AllDiceHaveTheSameValue(_dice),
      "Dice are not a straight");
  }

  private static bool AllDiceHaveTheSameValue(IEnumerable<DiceValue> destination)
  {
    return destination.GroupBy(v => v).Count() == 1;
  }
}

internal class PlayerIsInTurn : Validator
{
  private readonly int       _playerId;
  private readonly GameState _state;

  public PlayerIsInTurn(GameState state, int playerId)
  {
    _state    = state;
    _playerId = playerId;
  }

  public override ValidationResult IsSatisfied()
  {
    return new ValidationResult(_state.PlayerInTurn == _playerId,
      new PlayedOutOfTurn(_playerId, _state.PlayerInTurn));
  }
}

internal class PlayerCanPass : Validator
{
  private readonly Game _game;
  private readonly int  _playerId;

  public PlayerCanPass(Game game, int playerId)
  {
    _game     = game;
    _playerId = playerId;
  }

  public override ValidationResult IsSatisfied()
  {
    return new ValidationResult(
      _game.Current.LastEventsWhere(typeof(GameEvents.V2.DiceRolled))
      ||
      _game.Current.LastEventsWhere(typeof(GameEvents.V1.DiceRolled))
      ||
      _game.Current.LastEventsWhere(typeof(GameEvents.V2.DiceKept)) ||
      _game.Current.LastEventsWhere(typeof(GameEvents.V1.DiceKept)),
      new PassedWithoutRolling(_playerId));
  }
}

internal static class EnumerableExtensions
{
  public static bool LastEventsWhere<T>(
    this IEnumerable<T> events,
    IList<Type>         expectedEvents)
  {
    var itemList = events.Where(i => i is not IErrorEvent).Select(e => e!.GetType()).Reverse().ToList();

    return !expectedEvents.Where((t, index) => itemList[index] != t).Any();
  }

  public static bool LastEventsWhere<T>(
    this IEnumerable<T> events,
    Type                expectedEvent)
  {
    return events.LastEventsWhere(new[] { expectedEvent });
  }
}
