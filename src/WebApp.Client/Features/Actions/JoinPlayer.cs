using BlazorState;
using Farkle.Spa.Services;
using WebApp.Client.Services;

namespace WebApp.Client.Features;

public partial class GameState
{
  public static class JoinPlayer
  {
    // TODO: refactor playerId to player
    public record Action(PlayerId PlayerId, PlayerName PlayerName) : IAction;
    
    public class Handler(IStore store, IGameService service) : ActionHandler<Action>(store)
    {
      private WebApp.Client.Features.GameState State => Store.GetState<WebApp.Client.Features.GameState>();
      
      public override async Task Handle(Action action, CancellationToken aCancellationToken)
      {
        // TODO: refactor signature
        await service.JoinPlayerAsync(State.GameId, action.PlayerId, action.PlayerName);
        
        State.PlayerId   = action.PlayerId;
        State.PlayerName = action.PlayerName;
      }
    }
  }
}
