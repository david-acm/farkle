using BlazorState;
using Farkle.Spa.Features;

namespace Farkle.Spa.Components;

public partial class GameTitle : BlazorStateComponent
{
  public int GameId => GetState<GameState>().GameId;
}
