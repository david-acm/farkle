using Farkle.Realtime;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Farkle.Infrastructure.Realtime;

/// <summary>
/// Wires real-time delivery: SignalR and the <see cref="GameHub"/> endpoint. The broadcast itself is
/// done directly through <c>IHubContext&lt;GameHub&gt;</c> by <c>GameNotifier</c> in the core (ADR
/// 0005) — there is no broadcaster port/adapter to register here.
/// </summary>
public static class RealtimeServiceExtensions
{
  public static IServiceCollection AddFarkleRealtime(this IServiceCollection services)
  {
    services.AddSignalR();
    return services;
  }

  /// <summary>Maps the SignalR hub clients connect to for live turn/table updates.</summary>
  public static IEndpointRouteBuilder MapFarkleRealtime(this IEndpointRouteBuilder endpoints)
  {
    endpoints.MapHub<GameHub>("/hubs/game");
    return endpoints;
  }
}
