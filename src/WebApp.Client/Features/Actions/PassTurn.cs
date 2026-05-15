using BlazorState;
using WebApp.Client.Services;

namespace WebApp.Client.Features;

public partial class GameState
{
  public class PassTurn
  {
    public record Action : IAction;

    public class Handler(IStore store, IGameService service) : ActionHandler<Action>(store)
    {
      private GameState State => Store.GetState<GameState>();

      public override async Task Handle(Action action, CancellationToken aCancellationToken)
      {
        await service.PassTurnAsync(State.GameId, State.PlayerId);

        // Reset dice and turn score for the next turn.
        State.DiceInPlay.Clear();
        State.TurnScore = new(0);
      }
    }
  }
}
