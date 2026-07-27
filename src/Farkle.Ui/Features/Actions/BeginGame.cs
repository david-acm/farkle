using BlazorState;
using WebApp.Client.Telemetry;
using WebApp.Client.Services;

namespace WebApp.Client.Features;

public partial class GameState
{
  public static class BeginGame
  {
    public record Action : IAction, IServerCommandIntent;

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
        State.TurnNumber      = lobby.TurnNumber; // #244
        State.Scoreboard      = lobby.Roster
          .Select(p => new PlayerStanding(p.PlayerId, p.Name, 0, p.Color))
          .ToList();
        // #286 — play begins with the first player awaiting a roll, nothing committed yet.
        State.PlayStage        = Farkle.SharedKernel.Turns.GameStage.Rolling;
        State.HasActedThisTurn = false;
      }
    }
  }
}
