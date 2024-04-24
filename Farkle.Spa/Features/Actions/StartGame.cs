using BlazorState;
using Farkle.Spa.Services;

namespace Farkle.Spa.Features;

public partial class GameState
{
  public static partial class StartGame
  {
    public record Action(GameId GameId) : IAction;
    
    public class Handler(IStore store, IGameService service) : ActionHandler<Action>(store)
    {
      private GameState State => Store.GetState<GameState>();
      
      public override async Task Handle(Action action, CancellationToken aCancellationToken)
      {
        var response = await service.StartGameAsync(action.GameId);
        
        State.GameId = new(response);
      }
    }
  }
}
