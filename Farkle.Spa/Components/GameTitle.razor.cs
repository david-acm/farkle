using Microsoft.AspNetCore.Components;

namespace Farkle.Spa.Components;

public partial class GameTitle : ComponentBase
{
  private            int _gameId;
  [Parameter] public int GameId { get; set; }
  
  protected override void OnInitialized()
  {
    _gameId = GameId;
    base.OnInitialized();
  }
  
  protected override void OnParametersSet()
  {
    _gameId = GameId;
    base.OnParametersSet();
  }
  
}
