using BlazorState;
using WebApp.Client.Features;

namespace WebApp.Client.Pages.Game.Components;

public partial class GameTitle : BlazorStateComponent
{
  public int GameId => GetState<GameState>().GameId;
}
