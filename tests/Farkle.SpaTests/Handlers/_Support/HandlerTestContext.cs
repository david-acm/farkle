using BlazorState;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using WebApp.Client.Features;
using WebApp.Client.Services;

namespace Farkle.SpaTests.Handlers;

/// <summary>
/// Minimal host for BlazorState action handlers. No bUnit, no DOM.
/// Use when the test's question is "what does the store look like after
/// dispatching this action against a mocked IGameService?".
/// </summary>
public abstract class HandlerTestContext : IDisposable
{
  private readonly IServiceScope _scope;

  protected readonly IGameService GameService;

  protected HandlerTestContext()
  {
    var services = new ServiceCollection();
    GameService = Mock.Of<IGameService>();
    services.AddScoped<IGameService>(_ => GameService);
    services.AddLogging(b => b.AddDebug());
    services.AddBlazorState(o => o.Assemblies = [typeof(GameState).Assembly]);

    var provider = services.BuildServiceProvider();
    _scope = provider.CreateScope();
  }

  protected ISender   Sender => _scope.ServiceProvider.GetRequiredService<ISender>();
  protected IStore    Store  => _scope.ServiceProvider.GetRequiredService<IStore>();
  protected GameState State  => Store.GetState<GameState>();

  public void Dispose() => _scope.Dispose();
}
