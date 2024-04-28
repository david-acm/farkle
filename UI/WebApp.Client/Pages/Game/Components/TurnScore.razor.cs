using BlazorState;
using WebApp.Client.Features;

namespace WebApp.Client.Pages.Game.Components;

public partial class TurnScore : BlazorStateComponent
{
  private GameState GameState => GetState<GameState>();
  
  public int Value => GameState.TurnScore.Value;
}
