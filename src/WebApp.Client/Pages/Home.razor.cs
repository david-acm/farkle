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
  private int    _joinId    = 0;

  // STUB (commit 1): handlers wired up with the cards in commit 2.
  private Task StartNewGameAsync()
  {
    _ = _startName;
    _ = Logger;
    return Task.CompletedTask;
  }

  private void JoinExistingGame()
  {
    _ = _joinName;
    _ = _joinId;
    _ = Nav;
  }
}
