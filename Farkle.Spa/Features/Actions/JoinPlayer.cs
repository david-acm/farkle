using BlazorState;
using Farkle.Spa.Services;
using Mapster;

namespace Farkle.Spa.Features;

public partial class GameState
{
  public static class JoinPlayer
  {
    // TODO: refactor playerId to player
    public record Action(PlayerId PlayerId, PlayerName PlayerName) : IAction;
    
    public class Handler(IStore store, IGameService service) : ActionHandler<Action>(store)
    {
      private GameState State => Store.GetState<GameState>();
      
      public override async Task Handle(Action action, CancellationToken aCancellationToken)
      {
        // TODO: refactor signature
        await service.JoinPlayerAsync(State.GameId, action.PlayerId, action.PlayerName);
        
        // TODO: refactor playerId to player
        State.PlayerId = action.PlayerId;
      }
    }
  }
}
