using BlazorState;
using Farkle.Ui.Features;

namespace Farkle.Ui.Pages.Game.Components;

public partial class GameTitle : BlazorStateComponent
{
  public int GameId => GetState<GameState>().GameId;
}
