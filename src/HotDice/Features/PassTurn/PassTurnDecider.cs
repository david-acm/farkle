using System.Collections.Immutable;
using HotDice.Domain;
using HotDice.Domain.GameAggregate;
using HotDice.SharedKernel.Turns;

namespace HotDice.Features.PassTurn;

// Pure decision for the PassTurn slice: bank the turn score and rotate to the next player,
// declaring a win at or above the winning score. "Can I pass?" is now a pure state check
// (state.HasActedThisTurn) via the shared TurnActionPolicy — no event-history peeking.
internal static class PassTurnDecider
{
  // A player wins once their banked total reaches this on a pass (#178).
  private const int WinningScore = 5_000;

  public static IEnumerable<object> Decide(PassTurnCommand command, GameState state)
  {
    var inTurn = new PlayerIsInTurn(state, command.PlayerId).IsSatisfied();
    if (!inTurn.IsValid)
      return [inTurn.FailedValidationEvent];

    var availability = TurnActionPolicy.Evaluate(
      state.GameStage, state.HasActedThisTurn, selectionScores: false);
    if (!availability.CanPass)
      return [new GameEvents.V1.PassedWithoutRolling(command.PlayerId)];

    var newScore = state.GameScoreFor(command.PlayerId) + state.TurnScore;

    var events = new List<object>
    {
      new GameEvents.V1.TurnPassed(command.PlayerId, PlayerOrderAfter(command.PlayerId, state), newScore)
    };

    if (newScore >= WinningScore)
      events.Add(new GameEvents.V1.GameWon(command.PlayerId, newScore));

    return events;
  }

  // Rotate the passing player to the back of the order (the next player moves to the front).
  private static ImmutableArray<Player> PlayerOrderAfter(int playerId, GameState state)
  {
    var player = state.GetPlayer(playerId);
    return state.Players.Remove(player).Add(player);
  }
}
