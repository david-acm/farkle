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
        try
        {
          await service.PassTurnAsync(State.GameId, State.PlayerId);
        }
        catch
        {
          // Pass may fail if game state is unexpected (e.g. non-scoring keep was rejected).
          // Still reset UI so the player isn't stuck.
        }

        // Reset dice and turn score for the next turn.
        State.DiceInPlay.Clear();
        State.TurnScore = new(0);
      }
    }
  }
}
