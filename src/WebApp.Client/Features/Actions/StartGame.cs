using BlazorState;
using WebApp.Client.Services;

namespace WebApp.Client.Features;

public partial class GameState
{
  public static partial class StartGame
  {
    public record Action(GameId GameId) : IAction;
    
    public class Handler(IStore store, IGameService service) : ActionHandler<Action>(store)
    {
      private WebApp.Client.Features.GameState State => Store.GetState<WebApp.Client.Features.GameState>();
      
      public override async Task Handle(Action action, CancellationToken aCancellationToken)
      {
        var response = await service.StartGameAsync(action.GameId);
        
        State.GameId = new(response);
      }
    }
  }
}
