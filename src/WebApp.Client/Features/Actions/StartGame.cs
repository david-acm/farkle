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
        try
        {
          var response = await service.StartGameAsync(action.GameId);
          State.GameId = new(response);
        }
        catch
        {
          // Game may already exist (re-navigation). Use the requested ID so the
          // component can still render and subsequent actions work correctly.
          State.GameId = action.GameId;
        }
      }
    }
  }
}
