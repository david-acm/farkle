using BlazorState;
using WebApp.Client.Telemetry;
using WebApp.Client.Services;

namespace WebApp.Client.Features;

public partial class GameState
{
  public static class JoinPlayer
  {
    public record Action(PlayerName PlayerName) : IAction, IServerCommandIntent;

    public class Handler(IStore store, IGameService service) : ActionHandler<Action>(store)
    {
      private WebApp.Client.Features.GameState State => Store.GetState<WebApp.Client.Features.GameState>();

      public override async Task Handle(Action action, CancellationToken aCancellationToken)
      {
        var response = await service.JoinPlayerAsync(State.GameId, action.PlayerName);

        State.PlayerId        = new PlayerId(response.Id);
        State.PlayerName      = action.PlayerName;
        State.CurrentPlayerId = response.CurrentPlayerId;
        State.HostPlayerId    = response.HostPlayerId;
        State.GameStage       = response.Stage;
        State.Roster          = (response.Roster ?? [])
          .Select(p => new PlayerStanding(p.PlayerId, p.Name, 0, p.Color))
          .ToList();
        // Seed scoreboard from the lobby roster; PassTurn responses replace it with authoritative data.
        State.Scoreboard = State.Roster.Count > 0
          ? State.Roster
          : [new PlayerStanding(response.Id, action.PlayerName.Value, 0)];
      }
    }
  }
}
