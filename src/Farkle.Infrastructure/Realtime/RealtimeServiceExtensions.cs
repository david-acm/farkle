using Farkle.Application;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;

namespace Farkle.Infrastructure.Realtime;

/// <summary>
/// Wires real-time delivery: SignalR plus the <see cref="IGameEventBroadcaster"/> implementation
/// that pushes game events to clients. The actual broadcasts are triggered by the Eventuous
/// broadcast subscription (see AddFarkleEventStore), keeping the HTTP endpoints broadcast-free.
/// </summary>
public static class RealtimeServiceExtensions
{
  public static IServiceCollection AddFarkleRealtime(this IServiceCollection services)
  {
    services.AddSignalR();
    // Singleton: it only wraps the singleton IHubContext<GameHub>, consumed by the singleton
    // Eventuous broadcast subscription (GameBroadcastHandler), not per-request.
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
