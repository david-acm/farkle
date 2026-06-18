using Azure.Monitor.OpenTelemetry.AspNetCore;
using Eventuous.Diagnostics.OpenTelemetry;
using OpenTelemetry.Instrumentation.AspNetCore;
using OpenTelemetry.Logs;
using OpenTelemetry.Trace;

namespace WebApp.Telemetry;

/// <summary>
/// #216 — wires OpenTelemetry to Application Insights via the Azure Monitor distro: requests +
/// dependencies + Eventuous produce/consume spans (so the async event-store handlers correlate
/// back to the originating request), metrics, and logs. Gated on the connection string so local
/// dev / tests are a no-op (telemetry simply isn't exported).
/// </summary>
public static class FarkleTelemetryExtensions
{
  public static IServiceCollection AddFarkleTelemetry(this IServiceCollection services, string? connectionString)
  {
    if (string.IsNullOrWhiteSpace(connectionString))
      return services;

    services.AddOpenTelemetry()
      .UseAzureMonitor(o => o.ConnectionString = connectionString)
      .WithTracing(tracing => tracing
        // Postgres (Npgsql's built-in ActivitySource) for DB dependency spans, and Eventuous'
        // command-service / event-store / subscription spans. Eventuous persists the W3C trace
        // context into event metadata on append and restores it on consume, so the projector /
        // broadcaster / telemetry handlers link to the request that produced the event.
        .AddSource("Npgsql")
        .AddEventuousTracing());

    // Domain-event logs (carrying the "EventType" attribute from GameTelemetryHandler) become
    // Application Insights customEvents — the mapping lives here in the host, so the app/core
    // keeps emitting plain ILogger events with no telemetry coupling.
    services.ConfigureOpenTelemetryLoggerProvider(logging => logging.AddProcessor(new DomainEventLogProcessor()));

    // #216 — tag request spans with session.id = game and enduser.id = player (synthetic, no PII)
    // from the route, so requests + their dependency spans are sliceable by game and player.
    // EnrichWithHttpResponse runs after routing, so the {gameId}/{playerId} route values are set.
    services.Configure<AspNetCoreTraceInstrumentationOptions>(options =>
      options.EnrichWithHttpResponse = (activity, httpResponse) =>
      {
        var routeValues = httpResponse.HttpContext.Request.RouteValues;
        if (routeValues.TryGetValue("gameId", out var gameId) && gameId is not null)
        {
          activity.SetTag("session.id", $"g{gameId}");
          if (routeValues.TryGetValue("playerId", out var playerId) && playerId is not null)
            activity.SetTag("enduser.id", $"g{gameId}-p{playerId}");
        }
      });

    return services;
  }
}
