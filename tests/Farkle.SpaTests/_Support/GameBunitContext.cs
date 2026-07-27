using Bunit;
using BlazorState;
using Microsoft.AspNetCore.Components;
using Moq;
using MudBlazor.Services;
using WebApp.Client;
using WebApp.Client.Services;
using static Farkle.Contracts.HttpResponses;

namespace Farkle.SpaTests;

// Implements IAsyncLifetime so xUnit tears the bUnit context down asynchronously.
// MudBlazor inputs that intercept keystrokes register KeyInterceptorService, which is
// IAsyncDisposable-only; a synchronous Dispose would throw on teardown.
public class GameBunitContext : BunitContext, Xunit.IAsyncLifetime
{
  protected readonly IGameService  GameService;
  protected readonly TestHubService HubService = new();

  Task Xunit.IAsyncLifetime.InitializeAsync() => Task.CompletedTask;

  async Task Xunit.IAsyncLifetime.DisposeAsync() => await ((IAsyncDisposable)this).DisposeAsync();

  protected GameBunitContext()
  {
    JSInterop.Mode = JSRuntimeMode.Loose;
    GameService    = Mock.Of<IGameService>();
    Services.AddScoped<IGameService>(_ => GameService);
    Services.AddScoped<IGameHubService>(_ => HubService);
    Services.AddScoped<IRotationCalculator>(_ => Mock.Of<IRotationCalculator>());
    Services.AddScoped<IShareService>(_ => Mock.Of<IShareService>());
    Services.AddMudServices();
    // The store's actions/handlers live in the shared UI library, not in the WASM host.
    Services.AddBlazorState(o => o.Assemblies = [typeof(ClientServiceExtensions).Assembly]);
  }

  protected void JoinGame<TComponent>(IRenderedComponent<TComponent> cut, string playerName = "Tester", int assignedPlayerId = 1)
    where TComponent : IComponent
  {
    Mock.Get(GameService)
      .Setup(s => s.JoinPlayerAsync(It.IsAny<int>(), It.IsAny<string>()))
      .ReturnsAsync(new JoinPlayerResponse(assignedPlayerId, assignedPlayerId));
    cut.Find("[placeholder='Your name']").Input(playerName);
    cut.FindAll("button").First(b => b.TextContent.Contains("Join")).Click();
  }

  // Joins into the lobby (game stays in WaitingForPlayers with the given roster).
  protected void JoinLobby<TComponent>(
    IRenderedComponent<TComponent> cut,
    string playerName = "Tester",
    int assignedPlayerId = 1,
    int hostPlayerId = 1,
    params LobbyPlayer[] roster)
    where TComponent : IComponent
  {
    var players = roster.Length > 0
      ? roster
      : [new LobbyPlayer(assignedPlayerId, playerName)];
    Mock.Get(GameService)
      .Setup(s => s.JoinPlayerAsync(It.IsAny<int>(), It.IsAny<string>()))
      .ReturnsAsync(new JoinPlayerResponse(
        assignedPlayerId, hostPlayerId, hostPlayerId, "WaitingForPlayers", players));
    cut.Find("[placeholder='Your name']").Input(playerName);
    cut.FindAll("button").First(b => b.TextContent.Contains("Join")).Click();
  }

  public class TestHubService : IGameHubService, IDisposable
  {
    // #221 — second arg is the originating command's trace id (null in tests unless specified).
    public event Action<PassTurnResponse, string?>? OnTurnChanged;
    public event Action<LobbyStateResponse, string?>? OnPlayerJoined;
    public event Action<LobbyStateResponse, string?>? OnGameBegan;
    public event Action<GameStateResponse, string?>? OnTableChanged;
    public event Action<GameStateResponse, string?>? OnDiceRolled;
    public bool IsConnected { get; private set; }

    public Task ConnectAsync(int gameId, int playerId, CancellationToken ct = default)
    {
      IsConnected = true;
      return Task.CompletedTask;
    }

    public Task DisconnectAsync(CancellationToken ct = default)
    {
      IsConnected = false;
      return Task.CompletedTask;
    }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    public void Dispose() { }
    public void RaiseTurnChanged(PassTurnResponse payload, string? traceId = null) => OnTurnChanged?.Invoke(payload, traceId);
    public void RaisePlayerJoined(LobbyStateResponse payload, string? traceId = null) => OnPlayerJoined?.Invoke(payload, traceId);
    public void RaiseGameBegan(LobbyStateResponse payload, string? traceId = null) => OnGameBegan?.Invoke(payload, traceId);
    public void RaiseTableChanged(GameStateResponse payload, string? traceId = null) => OnTableChanged?.Invoke(payload, traceId);
    public void RaiseDiceRolled(GameStateResponse payload, string? traceId = null) => OnDiceRolled?.Invoke(payload, traceId);
  }
}
