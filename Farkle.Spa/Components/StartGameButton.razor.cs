using Farkle.Spa.Services;
using Microsoft.AspNetCore.Components;

namespace Farkle.Spa.Components;

public partial class StartGameButton : ComponentBase
{
  [Inject]    public IGameService                             GameService    { get; set; } = null!;
  
  // TODO change to value object
  [Parameter] public EventCallback<int> OnGameStarted { get; set; }
  
  private async Task StartGameAsync()
  {
    var random = new Random();
    var gameId = random.Next(0, 999);
    var id = await GameService.StartGameAsync(gameId);
    await GameService.JoinPlayerAsync(gameId, 1, "David");
    
    await OnGameStarted.InvokeAsync(id);
  }
}

