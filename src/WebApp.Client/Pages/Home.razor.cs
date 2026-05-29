using BlazorState;
using Microsoft.AspNetCore.Components;
using WebApp.Client.Features;

namespace WebApp.Client.Pages;

public partial class Home : BlazorStateComponent
{
  [Inject]
  public NavigationManager Nav { get; set; } = null!;

  [Inject]
  public ILogger<Home> Logger { get; set; } = null!;

  private string _startName = string.Empty;
  private string _joinName  = string.Empty;
  private int    _joinId;

  private async Task StartNewGameAsync()
  {
    if (string.IsNullOrWhiteSpace(_startName)) return;

    await Mediator.Send(new GameState.CreateGame.Action());
    var id = GetState<GameState>().GameId.Value;
    Logger.LogInformation("Created game {GameId}; navigating with host name {Name}", id, _startName);
    Nav.NavigateTo($"/games/{id}?name={Uri.EscapeDataString(_startName)}");
  }

  private void JoinExistingGame()
  {
    if (string.IsNullOrWhiteSpace(_joinName) || _joinId <= 0) return;
    Nav.NavigateTo($"/games/{_joinId}?name={Uri.EscapeDataString(_joinName)}");
  }
}
