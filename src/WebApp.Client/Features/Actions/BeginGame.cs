using BlazorState;
using WebApp.Client.Services;

namespace WebApp.Client.Features;

public partial class GameState
{
  public static class BeginGame
  {
    public record Action : IAction;

    public class Handler(IStore store, IGameService service) : ActionHandler<Action>(store)
    {
      private GameState State => Store.GetState<GameState>();

      public override async Task Handle(Action action, CancellationToken aCancellationToken)
      {
        // Only the host can begin; the button is hidden for everyone else.
        var lobby = await service.BeginGameAsync(State.GameId, State.PlayerId);

        State.GameStage       = lobby.Stage;
        State.CurrentPlayerId = lobby.CurrentPlayerId;
        State.HostPlayerId    = lobby.HostPlayerId;
        State.Scoreboard      = lobby.Players
          .Select(p => new PlayerStanding(p.PlayerId, p.Name, 0))
          .ToList();
      }
    }
  }
}
