using Bunit;
using BlazorState;
using Microsoft.AspNetCore.Components;
using Moq;
using MudBlazor.Services;
using WebApp.Client.Services;
using static Farkle.Contracts.HttpResponses;

namespace Farkle.SpaTests;

public class GameBunitContext : BunitContext
{
  protected readonly IGameService  GameService;
  protected readonly TestHubService HubService = new();

  protected GameBunitContext()
  {
    JSInterop.Mode = JSRuntimeMode.Loose;
    GameService    = Mock.Of<IGameService>();
    Services.AddScoped<IGameService>(_ => GameService);
    Services.AddScoped<IGameHubService>(_ => HubService);
    Services.AddScoped<IRotationCalculator>(_ => Mock.Of<IRotationCalculator>());
    Services.AddMudServices();
    Services.AddBlazorState(o => o.Assemblies = [typeof(Program).Assembly]);
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
    public event Action<PassTurnResponse>? OnTurnChanged;
    public event Action<LobbyStateResponse>? OnPlayerJoined;
    public event Action<LobbyStateResponse>? OnGameBegan;
    public Task ConnectAsync(int gameId) => Task.CompletedTask;
    public Task DisconnectAsync() => Task.CompletedTask;
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    public void Dispose() { }
    public void RaiseTurnChanged(PassTurnResponse payload) => OnTurnChanged?.Invoke(payload);
    public void RaisePlayerJoined(LobbyStateResponse payload) => OnPlayerJoined?.Invoke(payload);
    public void RaiseGameBegan(LobbyStateResponse payload) => OnGameBegan?.Invoke(payload);
  }
}
