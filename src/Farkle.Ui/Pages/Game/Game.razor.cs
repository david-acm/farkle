using System.Text.Json;
using BlazorState;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using WebApp.Client.Features;
using WebApp.Client.Services;
using static Farkle.Contracts.HttpResponses;

namespace WebApp.Client.Pages.Game;

public partial class Game : GameStateComponent, IAsyncDisposable
{
  private string _playerName = string.Empty;

  [Inject]
  public ILogger<Game> Logger { get; set; } = null!;

  [Inject]
  public IGameHubService GameHubService { get; set; } = null!;

  [Inject]
  public IJSRuntime Js { get; set; } = null!;

  [Parameter]
  public int ParameterGameId { get; set; } = 0;

  [SupplyParameterFromQuery(Name = "name")]
  public string? JoinName { get; set; }

  private bool _autoJoinAttempted;
  private bool _restoreAttempted;
  private bool _hubConnected;

  private GameId    GameId    => new(ParameterGameId);

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
      _autoJoinAttempted = false;
      _restoreAttempted  = false;
      _hubConnected      = false;
    }

    // Seed the game id — the game itself was created server-side by the landing page.
    await Mediator.Send(new GameState.SetGameId.Action(GameId));

    // Restore an existing session (from a previous join in this browser tab) before
    // considering auto-join, so a refresh resumes the same player rather than joining
    // again — re-joining would add a duplicate player to the game.
    if (!HasJoined && !_restoreAttempted)
    {
      _restoreAttempted = true; // set BEFORE awaiting to avoid re-entry on re-render
      await TryRestoreSessionAsync();
    }

    // The player's name is carried from the landing page via the ?name= query param,
    // so auto-join without re-prompting (only when we didn't restore an existing session).
    if (!HasJoined && !_autoJoinAttempted && !string.IsNullOrWhiteSpace(JoinName))
    {
      _autoJoinAttempted = true; // set BEFORE awaiting to prevent double-join on re-render
      _playerName = JoinName!;
      await JoinAsync();
    }

    await base.OnParametersSetAsync();
  }

  private async Task JoinAsync()
  {
    if (string.IsNullOrWhiteSpace(_playerName)) return;
    try
    {
      await Mediator.Send(new GameState.JoinPlayer.Action(new(_playerName)));
      await PersistSessionAsync();
      await ConnectHubAsync();
    }
    catch (Exception ex)
    {
      Logger.LogWarning(ex, "JoinPlayer failed for game {GameId}", GameId);
    }
  }

  // Re-hydrates the player's view on refresh/reconnect from sessionStorage + the
  // server snapshot. On success the hub is (re)joined; if the game is gone the stale
  // session is cleared so the join form reappears.
  private async Task TryRestoreSessionAsync()
  {
    try
    {
      var stored = await LoadSessionAsync(ParameterGameId);
      if (stored is null) return;

      await Mediator.Send(new GameState.RestoreGameState.Action(stored.PlayerId, stored.Name));

      if (HasJoined)
        await ConnectHubAsync();
      else
        await ClearSessionAsync(ParameterGameId); // game no longer exists
    }
    catch (Exception ex)
    {
      Logger.LogWarning(ex, "Session restore failed for game {GameId}", GameId);
    }
  }

  private async Task ConnectHubAsync()
  {
    if (_hubConnected) return;
    _hubConnected = true;
    GameHubService.OnTurnChanged  += HandleTurnChanged;
    GameHubService.OnPlayerJoined += HandlePlayerJoined;
    GameHubService.OnGameBegan    += HandleGameBegan;
    GameHubService.OnTableChanged += HandleTableChanged;
    GameHubService.OnDiceRolled   += HandleDiceRolled;
    await GameHubService.ConnectAsync(ParameterGameId, GameState.PlayerId.Value);

    // #236 — close the connect race. A broadcast (e.g. GameBegan) sent between our HTTP join and
    // our SignalR group-join is missed: SignalR pushes are one-shot and never replayed, so the
    // just-joined client can stay stuck on the lobby/waiting view while everyone else moved on
    // (intermittent against real network latency; ~never on localhost). Now that we're in the
    // group, reconcile against the server snapshot so any missed transition self-heals; every
    // subsequent broadcast keeps us current.
    if (HasJoined)
      await Mediator.Send(new GameState.RestoreGameState.Action(
        GameState.PlayerId.Value, GameState.PlayerName.Value));

    // #216 — by now PlayerId is set (via join or restore); tag browser telemetry with the player/game.
    await SetTelemetryUserAsync();
  }

  // #216 — set the App Insights browser user (player) + account (game) so RUM is sliceable by
  // player and game. Synthetic ids only (no display names / PII). Best-effort.
  private async Task SetTelemetryUserAsync()
  {
    if (GameState.PlayerId.Value <= 0) return;
    try
    {
      var userId = $"g{ParameterGameId}-p{GameState.PlayerId.Value}";
      await Js.InvokeVoidAsync("farkleTelemetry.setPlayer", userId, ParameterGameId.ToString());
    }
    catch (JSException ex)
    {
      Logger.LogDebug(ex, "Setting telemetry user failed (non-fatal) for game {GameId}", GameId);
    }
  }

  private static string SessionKey(int gameId) => $"farkle:game:{gameId}";

  private async Task PersistSessionAsync()
  {
    var payload = JsonSerializer.Serialize(
      new StoredSession { PlayerId = GameState.PlayerId.Value, Name = GameState.PlayerName.Value });
    await Js.InvokeVoidAsync("sessionStorage.setItem", SessionKey(ParameterGameId), payload);
  }

  private async Task<StoredSession?> LoadSessionAsync(int gameId)
  {
    var json = await Js.InvokeAsync<string?>("sessionStorage.getItem", SessionKey(gameId));
    if (string.IsNullOrEmpty(json)) return null;
    try
    {
      var stored = JsonSerializer.Deserialize<StoredSession>(json);
      return stored is { PlayerId: > 0 } ? stored : null;
    }
    catch (JsonException)
    {
      return null;
    }
  }

  private async Task ClearSessionAsync(int gameId) =>
    await Js.InvokeVoidAsync("sessionStorage.removeItem", SessionKey(gameId));

  private sealed class StoredSession
  {
    public int    PlayerId { get; set; }
    public string Name     { get; set; } = string.Empty;
  }

  // #221 — the traceId arg is the originating command's App Insights operation_Id, carried over
  // SignalR; it's threaded onto the dispatched action so the UI.* event links back to the command.
  private async void HandleTurnChanged(PassTurnResponse payload, string? traceId)
  {
    try
    {
      await InvokeAsync(async () =>
        await Mediator.Send(new GameState.RemoteTurnChanged.Action(payload, traceId)));
    }
    catch (Exception ex)
    {
      Logger.LogWarning(ex, "RemoteTurnChanged failed for game {GameId}", GameId);
    }
  }

  private async void HandlePlayerJoined(LobbyStateResponse payload, string? traceId)
  {
    try
    {
      await InvokeAsync(async () =>
        await Mediator.Send(new GameState.RemotePlayerJoined.Action(payload, traceId)));
    }
    catch (Exception ex)
    {
      Logger.LogWarning(ex, "RemotePlayerJoined failed for game {GameId}", GameId);
    }
  }

  private async void HandleGameBegan(LobbyStateResponse payload, string? traceId)
  {
    try
    {
      await InvokeAsync(async () =>
        await Mediator.Send(new GameState.RemoteGameBegan.Action(payload, traceId)));
    }
    catch (Exception ex)
    {
      Logger.LogWarning(ex, "RemoteGameBegan failed for game {GameId}", GameId);
    }
  }

  private async void HandleTableChanged(GameStateResponse payload, string? traceId)
  {
    try
    {
      await InvokeAsync(async () =>
        await Mediator.Send(new GameState.RemoteTableChanged.Action(payload, CausedByOperationId: traceId)));
    }
    catch (Exception ex)
    {
      Logger.LogWarning(ex, "RemoteTableChanged failed for game {GameId}", GameId);
    }
  }

  // A roll snapshot — same table update as TableChanged but animate the dice (#167).
  private async void HandleDiceRolled(GameStateResponse payload, string? traceId)
  {
    try
    {
      await InvokeAsync(async () =>
        await Mediator.Send(new GameState.RemoteTableChanged.Action(payload, Animate: true, CausedByOperationId: traceId)));
    }
    catch (Exception ex)
    {
      Logger.LogWarning(ex, "RemoteDiceRolled failed for game {GameId}", GameId);
    }
  }

  public async ValueTask DisposeAsync()
  {
    GameHubService.OnTurnChanged -= HandleTurnChanged;
    GameHubService.OnPlayerJoined -= HandlePlayerJoined;
    GameHubService.OnGameBegan -= HandleGameBegan;
    GameHubService.OnTableChanged -= HandleTableChanged;
    GameHubService.OnDiceRolled -= HandleDiceRolled;
    await GameHubService.DisconnectAsync();
  }
}
