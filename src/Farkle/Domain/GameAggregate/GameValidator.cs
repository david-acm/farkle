using Farkle.SharedKernel.Turns;
using static Farkle.Domain.GameAggregate.GameEvents.V1;

namespace Farkle.Domain.GameAggregate;

// The composite validators the pure #301 deciders use to express validation-as-events. The old
// Eventuous ValidatePreconditions(Game, @event) switch and the game.Current-scanning PlayerCanPass
// are gone (ADR 0004): each decider composes the validators it needs, and PassTurn reads the pure
// GameState.HasActedThisTurn flag instead of scanning event history.
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
    // #286 — "can I roll now?" is the shared policy's stage rule (a roll is only valid while
    // awaiting a roll — no second roll before a keep). hasActed / selectionScores don't gate Roll.
    var availability = TurnActionPolicy.Evaluate(
      _state.GameStage, hasActedThisTurn: false, selectionScores: false);
    return new ValidationResult(availability.CanRoll, new RolledTwice(_playerId));
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

// #159 — a die can be set aside only if a not-yet-set-aside copy is still on the table
// (multiset semantics: setting aside is a non-destructive overlay on TableCenter).
internal class DieIsAvailableToSetAside : Validator
{
  private readonly GameState _state;
  private readonly int       _playerId;
  private readonly int       _die;

  public DieIsAvailableToSetAside(GameState state, int playerId, int die)
  {
    _state    = state;
    _playerId = playerId;
    _die      = die;
  }

  public override ValidationResult IsSatisfied()
  {
    var die        = DieValue.FromValue(_die);
    var onTable    = _state.TableCenter.Count(d => d == die);
    var alreadySet = _state.DiceSetAside.Count(d => d == die);
    return new ValidationResult(onTable > alreadySet,
      new DieNotAvailableToSetAside(_playerId, _die));
  }
}

// #159 — a die can be put back only if it is currently set aside.
internal class DieIsSetAside : Validator
{
  private readonly GameState _state;
  private readonly int       _playerId;
  private readonly int       _die;

  public DieIsSetAside(GameState state, int playerId, int die)
  {
    _state    = state;
    _playerId = playerId;
    _die      = die;
  }

  public override ValidationResult IsSatisfied()
  {
    return new ValidationResult(_state.DiceSetAside.Contains(DieValue.FromValue(_die)),
      new DieNotSetAside(_playerId, _die));
  }
}

public record GameAlreadyStarted(int Id);

public record GameHasNotStarted(GameStage GameStage);

public record DiceNotAllowedToBeKept(string Reason, IEnumerable<int> Dice) : IErrorEvent;

internal class CanKeepDice : Validator
{
  private readonly IEnumerable<DieValue> _dice;

  public CanKeepDice(Dice dice)
  {
    _dice = dice.DiceValues;
  }

  public override ValidationResult IsSatisfied()
  {
    // Delegates to the shared ScoreCalculator so "what's keepable" has one definition.
    return new ValidationResult(
      Farkle.SharedKernel.Scoring.ScoreCalculator.CanKeep(_dice.Select(d => d.Value).ToList()),
      new DiceNotAllowedToBeKept("Dice are not ones or fives",
        _dice.ToPrimitiveArray()));
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
  internal const int Minimum = 1;
  private readonly GameState _state;

  public HasMinimumPlayers(GameState state)
  {
    _state = state;
  }

  public override ValidationResult IsSatisfied()
  {
    return new ValidationResult(_state.Players.Length >= Minimum,
      new NotEnoughPlayers(_state.Players.Length, Minimum));
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

// #286 — "can I pass now?" is the shared policy's Pass rule: you may pass only once you've acted
// this turn. The signal is the pure GameState.HasActedThisTurn flag (set by roll/keep, reset on
// pass), so the check is a pure (state) decision with no event-history scan.
internal class PlayerCanPass : Validator
{
  private readonly GameState _state;
  private readonly int       _playerId;

  public PlayerCanPass(GameState state, int playerId)
  {
    _state    = state;
    _playerId = playerId;
  }

  public override ValidationResult IsSatisfied()
  {
    var availability = TurnActionPolicy.Evaluate(
      _state.GameStage, _state.HasActedThisTurn, selectionScores: false);
    return new ValidationResult(availability.CanPass, new PassedWithoutRolling(_playerId));
  }
}
