using Farkle.Application;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;

namespace Farkle.Infrastructure.Realtime;

/// <summary>
/// Wires real-time delivery: SignalR plus the <see cref="IGameEventBroadcaster"/> implementation
/// that pushes game events to clients. Broadcasts are triggered post-commit by the endpoints via
/// <c>GameNotifier</c> (ADR 0004), which reads the up-to-date Marten snapshot and pushes it.
/// </summary>
public static class RealtimeServiceExtensions
{
  public static IServiceCollection AddFarkleRealtime(this IServiceCollection services)
  {
    services.AddSignalR();
    // Singleton: it only wraps the singleton IHubContext<GameHub>.
    services.AddSingleton<IGameEventBroadcaster, SignalRGameEventBroadcaster>();
    return services;
  }

  /// <summary>Maps the SignalR hub clients connect to for live turn/table updates.</summary>
  public static IEndpointRouteBuilder MapFarkleRealtime(this IEndpointRouteBuilder endpoints)
  {
    endpoints.MapHub<GameHub>("/hubs/game");
    return endpoints;
  }
}
