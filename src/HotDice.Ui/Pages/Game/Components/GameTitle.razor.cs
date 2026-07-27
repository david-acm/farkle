using BlazorState;
using HotDice.Ui.Features;

namespace HotDice.Ui.Pages.Game.Components;

public partial class GameTitle : BlazorStateComponent
{
  public int GameId => GetState<GameState>().GameId;
}
