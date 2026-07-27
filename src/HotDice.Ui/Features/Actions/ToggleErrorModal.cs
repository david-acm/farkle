using BlazorState;

namespace HotDice.Ui.Features;

public partial class GameState
{
  public class ToggleErrorModal
  {
    public record Action(bool Show, string? ErrorMessage = null) : IAction;
    
    public class Handler(IStore store)
      : ActionHandler<Action>(store)
    {
      private HotDice.Ui.Features.GameState State => Store.GetState<HotDice.Ui.Features.GameState>();
      
      public override Task Handle(Action action, CancellationToken aCancellationToken)
      {
        State.ShowError = action.Show;
        
        if (action.ErrorMessage is not null)
        {
          State.ErrorMessage = action.ErrorMessage;
        }
        
        return Task.CompletedTask;
      }
    }
  }
}
