using BlazorState;
using Farkle.Spa.Features;

namespace Farkle.Spa.Components;

public partial class TurnScore : BlazorStateComponent
{
  private GameState GameState => GetState<GameState>();
  
  public int Value => GameState.TurnScore.Value;
}
