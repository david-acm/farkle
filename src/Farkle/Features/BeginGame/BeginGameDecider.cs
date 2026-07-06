using Farkle.Domain.GameAggregate;

namespace Farkle.Features.BeginGame;

// Pure decision for the BeginGame slice: the host moves the game out of the lobby into play.
// Composes the existing validator primitives (lobby is open, minimum players present, requester
// is the host) and returns the first failed-validation event, else GamePlayStarted.
internal static class BeginGameDecider
{
  public static IEnumerable<object> Decide(Command.BeginGame command, GameState state)
  {
    var result = new GameIsWaitingForPlayers(state)
      .And(new HasMinimumPlayers(state))
      .And(new RequesterIsHost(state, command.PlayerId))
      .IsSatisfied();

    if (!result.IsValid)
      return [result.FailedValidationEvent];

    return [new GameEvents.V1.GamePlayStarted(command.PlayerId)];
  }
}
