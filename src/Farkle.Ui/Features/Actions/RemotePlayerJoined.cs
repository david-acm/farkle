using BlazorState;
using WebApp.Client.Telemetry;
using static Farkle.Contracts.HttpResponses;

namespace WebApp.Client.Features;

public partial class GameState
{
  public static class RemotePlayerJoined
  {
    public record Action(LobbyStateResponse Payload, string? CausedByOperationId = null)
      : IAction, ICausedByBroadcast;

    public class Handler(IStore store) : ActionHandler<Action>(store)
    {
      private GameState State => Store.GetState<GameState>();

      public override Task Handle(Action action, CancellationToken aCancellationToken)
      {
        var p = action.Payload;
        State.GameStage    = p.Stage;
        State.HostPlayerId = p.HostPlayerId;
        State.Roster       = p.Roster
          .Select(lp => new PlayerStanding(lp.PlayerId, lp.Name, 0, lp.Color))
          .ToList();
        return Task.CompletedTask;
      }
    }
  }
}
