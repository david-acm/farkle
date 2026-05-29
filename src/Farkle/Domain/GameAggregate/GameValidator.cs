using Eventuous;
using static Farkle.Domain.GameAggregate.GameEvents.V1;

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
        state.GameStage == GameStage.WaitingForPlayers,
        new GameHasNotStarted(state.GameStage)),
      GamePlayStarted e =>
        new GameIsWaitingForPlayers(state)
          .And(new HasMinimumPlayers(state))
          .And(new RequesterIsHost(state, e.StartedByPlayerId))
          .IsSatisfied(),
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
  private readonly IEnumerable<DieValue> _dice;

  public DiceAreStair(Dice dice)
  {
    _dice = dice.DiceValues;
  }

  public override ValidationResult IsSatisfied()
  {
    return new ValidationResult(_dice.Count() == 6              &&
                                _dice.Contains(DieValue.One)   &&
                                _dice.Contains(DieValue.Two)   &&
                                _dice.Contains(DieValue.Three) &&
                                _dice.Contains(DieValue.Four)  &&
                                _dice.Contains(DieValue.Five)  &&
                                _dice.Contains(DieValue.Six),
      new DiceNotAllowedToBeKept("Dice are not a stair", _dice.ToPrimitiveArray())
    );
  }
}

[EventType("V1.GameAlreadyStarted")]
internal record GameAlreadyStarted(int Id);

[EventType("V1.GameHasNotStarted")]
internal record GameHasNotStarted(GameStage GameStage);

[EventType("V1.DiceNotAllowedToBeKept")]
internal record DiceNotAllowedToBeKept(string Reason, IEnumerable<int> Dice) : IErrorEvent;

internal class DiceAreOnesOrFives : Validator
{
  private readonly IEnumerable<DieValue> _dice;

  public DiceAreOnesOrFives(Dice dice)
  {
    _dice = dice.DiceValues;
  }

  public override ValidationResult IsSatisfied()
  {
    return new ValidationResult(_dice.All(d => d == DieValue.One || d == DieValue.Five),
      new DiceNotAllowedToBeKept("Dice are not ones or fives", _dice.ToPrimitiveArray()));
  }
}

internal class CanKeepDice : Validator
{
  private readonly IEnumerable<DieValue> _dice;

  public CanKeepDice(Dice dice)
  {
    _dice = dice.DiceValues;
  }

  public override ValidationResult IsSatisfied()
  {
    var diceContainOnesOrFives          = _dice.Any(d => d == DieValue.One || d == DieValue.Five);
    var thereAreThreeOrMoreRepeatedDice = _dice.GroupBy(d => d).MaxBy(d => d.Count())?.Count() >= 3;

    return new ValidationResult(
      diceContainOnesOrFives || thereAreThreeOrMoreRepeatedDice,
      new DiceNotAllowedToBeKept("Dice are not ones or fives",
        _dice.ToPrimitiveArray()));
  }
}

internal class DiceAreTrips : Validator
{
  private readonly IEnumerable<DieValue> _dice;

  public DiceAreTrips(Dice dice)
  {
    _dice = dice.DiceValues;
  }

  public override ValidationResult IsSatisfied()
  {
    return new ValidationResult(AreThree(_dice) && AllDiceHaveTheSameValue(_dice), $"The dice {_dice} are not trips.");
  }

  private static bool AreThree(IEnumerable<DieValue> destination)
  {
    return destination.Count() == 3;
  }

  private static bool AllDiceHaveTheSameValue(IEnumerable<DieValue> destination)
  {
    return destination.GroupBy(v => v).Count() == 1;
  }
}

internal class DiceAreStraight : Validator
{
  private readonly IEnumerable<DieValue> _dice;

  public DiceAreStraight(Dice dice)
  {
    _dice = dice.DiceValues;
  }

  public override ValidationResult IsSatisfied()
  {
    return new ValidationResult(_dice.Count() == 4 && AllDiceHaveTheSameValue(_dice),
      "Dice are not a straight");
  }

  private static bool AllDiceHaveTheSameValue(IEnumerable<DieValue> destination)
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

internal class GameIsWaitingForPlayers : Validator
{
  private readonly GameState _state;

  public GameIsWaitingForPlayers(GameState state)
  {
    _state = state;
  }

  public override ValidationResult IsSatisfied()
  {
    return new ValidationResult(_state.GameStage == GameStage.WaitingForPlayers,
      new GameAlreadyInPlay(_state.GameStage));
  }
}

internal class HasMinimumPlayers : Validator
{
  internal const int Minimum = 2;
  private readonly GameState _state;

  public HasMinimumPlayers(GameState state)
  {
    _state = state;
  }

  public override ValidationResult IsSatisfied()
  {
    return new ValidationResult(_state.Players.Length >= Minimum,
      new NotEnoughPlayers(_state.Players.Length));
  }
}

internal class RequesterIsHost : Validator
{
  private readonly GameState _state;
  private readonly int       _playerId;

  public RequesterIsHost(GameState state, int playerId)
  {
    _state    = state;
    _playerId = playerId;
  }

  public override ValidationResult IsSatisfied()
  {
    // The host is the first player to join (player id 1, the game creator).
    var hostId = _state.Players.IsEmpty ? 0 : _state.Players[0].Id;
    return new ValidationResult(_playerId == hostId,
      new OnlyHostCanStartGame(_playerId, hostId));
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
