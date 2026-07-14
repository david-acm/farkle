using Farkle.Domain.GameAggregate;
using Farkle.SharedKernel.Turns;

namespace Farkle.Features.JoinPlayer;

// Pure decision for the JoinPlayer slice. The next player id is derived from the current roster
// size; colour comes from the deterministic PlayerColors palette. Validation-as-events: joining
// before the lobby is open returns GameHasNotStarted.
internal static class JoinPlayerDecider
{
  public static IEnumerable<object> Decide(JoinPlayerCommand command, GameState state)
  {
    if (state.GameStage != GameStage.WaitingForPlayers)
      return [new GameHasNotStarted(state.GameStage)];

    var newId = state.Players.Length + 1;
    return [new GameEvents.V2.PlayerJoined(newId, command.Name, PlayerColors.For(newId))];
  }
}
