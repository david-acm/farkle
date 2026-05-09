using BlazorState;
using WebApp.Client.Services;

namespace WebApp.Client.Features;

public partial class GameState
{
  public class PassTurn
  {
    public record Action() : IAction;

    public class Handler(IStore store, IGameService service, ILogger<PassTurn> logger)
      : ActionHandler<Action>(store)
    {
      private GameState State => Store.GetState<GameState>();

      public override async Task Handle(Action action, CancellationToken aCancellationToken)
      {
        var response = await service.PassTurnAsync(State.GameId, State.PlayerId);

        logger.LogInformation("Turn passed. New score: {NewScore} Winner: {Winner}",
          response.NewScore, response.Winner?.Name);

        State.TurnScore  = new TurnScore(0);
        State.DiceInPlay = [];

        if (response.Winner is not null)
          State.WinnerName = response.Winner.Name;
      }
    }
  }
}
