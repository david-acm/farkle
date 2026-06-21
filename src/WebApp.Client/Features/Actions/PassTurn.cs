using BlazorState;
using WebApp.Client.Telemetry;
using WebApp.Client.Services;

namespace WebApp.Client.Features;

public partial class GameState
{
  public class PassTurn
  {
    public record Action : IAction, IServerCommandIntent;

    public class Handler(IStore store, IGameService service) : ActionHandler<Action>(store)
    {
      private GameState State => Store.GetState<GameState>();

      public override async Task Handle(Action action, CancellationToken aCancellationToken)
      {
        var response = await service.PassTurnAsync(State.GameId, State.PlayerId);

        State.CurrentPlayerId = response.CurrentPlayerId;
        State.TurnNumber = response.TurnNumber; // #244
        State.Scoreboard = (response.Scoreboard ?? [])
          .Select(p => new PlayerStanding(p.PlayerId, p.Name, p.Score, p.Color))
          .ToList();
        State.WinnerName = response.Winner?.Name;
        // Reset dice and turn score for the next turn.
        State.DiceInPlay.Clear();
        State.TurnScore = new(0);
      }
    }
  }
}
