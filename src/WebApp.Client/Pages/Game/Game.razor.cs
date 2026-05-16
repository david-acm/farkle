using BlazorState;
using Microsoft.AspNetCore.Components;
using WebApp.Client.Features;

namespace WebApp.Client.Pages.Game;

public partial class Game : BlazorStateComponent
{
  private string _playerName = string.Empty;

  [Inject]
  public ILogger<Game> Logger { get; set; } = null!;

  [Parameter]
  public int PlayerId { get; set; }

  [Parameter]
  public int ParameterGameId { get; set; } = 0;

  private GameId    GameId    => new(ParameterGameId);
  private GameState GameState => GetState<GameState>();

  private bool HasJoined => !string.IsNullOrEmpty(GameState.PlayerName.Value);

  protected override async Task OnParametersSetAsync()
  {
    try
    {
      await Mediator.Send(new GameState.StartGame.Action(GameId));
    }
    catch (Exception ex)
    {
      Logger.LogWarning(ex, "StartGame failed for game {GameId}", GameId);
    }
    await base.OnParametersSetAsync();
  }

  private async Task JoinAsync()
  {
    if (string.IsNullOrWhiteSpace(_playerName)) return;
    try
    {
      await Mediator.Send(new GameState.JoinPlayer.Action(new(PlayerId), new(_playerName)));
    }
    catch (Exception ex)
    {
      Logger.LogWarning(ex, "JoinPlayer failed for game {GameId}", GameId);
    }
  }
}
