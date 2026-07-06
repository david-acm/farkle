using BlazorState;
using WebApp.Client.Telemetry;
using WebApp.Client.Services;

namespace WebApp.Client.Features;

public partial class GameState
{
  public class KeepDice
  {
    public record Action() : IAction, IServerCommandIntent;

    public class Handler(IStore store, IGameService service) : ActionHandler<Action>(store)
    {
      private GameState State => Store.GetState<WebApp.Client.Features.GameState>();

      public override async Task Handle(Action action, CancellationToken aCancellationToken)
      {
        var response = await service.KeepDiceAsync(State.GameId, State.PlayerId, State.DiceSetAside);
        State.DiceInPlay.RemoveAll(d => d.IsSelected);

        State.TurnScore = new(response.TurnScore);

        // #286 — after keeping, the player may roll the remaining dice again (or pass); the
        // staged selection is consumed, so Keep goes disabled until a new scoring selection.
        State.PlayStage        = Farkle.SharedKernel.Turns.GameStage.Rolling;
        State.HasActedThisTurn = true;
      }
    }
  }
}
