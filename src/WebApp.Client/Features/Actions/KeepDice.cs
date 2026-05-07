using BlazorState;
using WebApp.Client.Services;

namespace WebApp.Client.Features;

public partial class GameState
{
  public class KeepDice
  {
    public record Action() : IAction;
    
    public class Handler(IStore store, IGameService service, ILogger<KeepDice> logger) : ActionHandler<Action>(store)
    {
      private GameState State => Store.GetState<WebApp.Client.Features.GameState>();
      
      public override async Task Handle(Action action, CancellationToken aCancellationToken)
      {
        var response = await service.KeepDiceAsync(State.GameId, State.PlayerId, State.DiceSetAside);
        foreach (var die in  State.DiceInPlay.Where(d => d.Identifier == "SetAside"))
        {
          die.Identifier = "Kept";
        }
       
        logger.LogInformation("Dice in play count: {currentCount}", State.DiceInPlay.Count);
        State.DiceSetAside.Clear();
        
        State.TurnScore = new(response.TurnScore);
      }
    }
  }
}
