using Farkle.Domain.GameAggregate;
using Farkle.SharedKernel.Turns;

namespace Farkle.Features.StartGame;

// The pure decision for the StartGame slice: (command, state) -> events. No framework types,
// no I/O — validation-as-events lives here too (starting an already-started game returns the
// error event instead of throwing). The slice endpoint (via IGameCreator's StartStream) simply
// appends whatever this returns. Kept pure by the decider-purity arch test.
internal static class StartGameDecider
{
  public static IEnumerable<object> Decide(StartGameCommand command, GameState state)
  {
    if (state.GameStage != GameStage.None)
      return [new GameAlreadyStarted(command.GameId)];

    return [new GameEvents.V1.GameStarted(command.GameId)];
  }
}
