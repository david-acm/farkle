using Bunit;
using BlazorState;
using Microsoft.AspNetCore.Components;
using Moq;
using MudBlazor.Services;
using WebApp.Client.Services;

namespace Farkle.SpaTests.GameTests;

public class GameBunitContext : BunitContext
{
  protected readonly IGameService GameService;

  protected GameBunitContext()
  {
    JSInterop.Mode = JSRuntimeMode.Loose;
    GameService    = Mock.Of<IGameService>();
    Services.AddScoped<IGameService>(_ => GameService);
    Services.AddScoped<IRotationCalculator>(_ => Mock.Of<IRotationCalculator>());
    Services.AddMudServices();
    Services.AddBlazorState(o => o.Assemblies = [typeof(Program).Assembly]);
  }

  protected void JoinGame<TComponent>(IRenderedComponent<TComponent> cut, string playerName = "Tester")
    where TComponent : IComponent
  {
    cut.Find("[placeholder='Your name']").Input(playerName);
    cut.FindAll("button").First(b => b.TextContent.Contains("Join")).Click();
  }
}
