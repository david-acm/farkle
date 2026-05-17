using BlazorState;
using Microsoft.AspNetCore.Components;
using WebApp.Client.Features;
using WebApp.Client.Services;
using static Farkle.Contracts.HttpResponses;

namespace WebApp.Client.Pages.Game;

public partial class Game : BlazorStateComponent, IAsyncDisposable
{
  private string _playerName = string.Empty;

  [Inject]
  public ILogger<Game> Logger { get; set; } = null!;

  [Inject]
  public IGameHubService GameHubService { get; set; } = null!;

  [Parameter]
  public int ParameterGameId { get; set; } = 0;

  private GameId    GameId    => new(ParameterGameId);
  private GameState GameState => GetState<GameState>();

  private bool HasJoined => !string.IsNullOrEmpty(GameState.PlayerName.Value);

  protected override async Task OnParametersSetAsync()
  {
    // When the user navigates to a different game within the same WASM session
    // (e.g. via Blazor Router), clear the stale player data so the join form
    // appears for the new game rather than inheriting the previous game's state.
    if (GameState.GameId.Value != 0 && GameState.GameId.Value != ParameterGameId)
    {
      await GameHubService.DisconnectAsync();
      await Mediator.Send(new GameState.LeaveGame.Action());
    }

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
      await Mediator.Send(new GameState.JoinPlayer.Action(new(_playerName)));
      GameHubService.OnTurnChanged += HandleTurnChanged;
      await GameHubService.ConnectAsync(ParameterGameId);
    }
    catch (Exception ex)
    {
      Logger.LogWarning(ex, "JoinPlayer failed for game {GameId}", GameId);
    }
  }

  private async void HandleTurnChanged(PassTurnResponse payload)
  {
    try
    {
      await InvokeAsync(async () =>
        await Mediator.Send(new GameState.RemoteTurnChanged.Action(payload)));
    }
    catch (Exception ex)
    {
      Logger.LogWarning(ex, "RemoteTurnChanged failed for game {GameId}", GameId);
    }
  }

  public async ValueTask DisposeAsync()
  {
    GameHubService.OnTurnChanged -= HandleTurnChanged;
    await GameHubService.DisconnectAsync();
  }
}
