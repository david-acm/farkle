using Azure.Monitor.OpenTelemetry.AspNetCore;
using OpenTelemetry.Instrumentation.AspNetCore;
using OpenTelemetry.Logs;
using OpenTelemetry.Trace;

namespace WebApp.Telemetry;

/// <summary>
/// #216 — wires OpenTelemetry to Application Insights via the Azure Monitor distro: requests +
/// dependencies + Marten/Wolverine event-store spans (so the async message handlers correlate back
/// to the originating request), metrics, and logs. Gated on the connection string so local dev /
/// tests are a no-op (telemetry simply isn't exported).
/// </summary>
public static class HotDiceTelemetryExtensions
{
  public static IServiceCollection AddHotDiceTelemetry(this IServiceCollection services, string? connectionString)
  {
    if (string.IsNullOrWhiteSpace(connectionString))
      return services;

    services.AddOpenTelemetry()
      .UseAzureMonitor(o => o.ConnectionString = connectionString)
      .WithTracing(tracing => tracing
        // Postgres (Npgsql's built-in ActivitySource) for DB dependency spans, plus Marten's event
        // store and Wolverine's message-bus ActivitySources. Marten/Wolverine propagate the W3C
        // trace context across the append→handle hop, so the async handlers (broadcast, telemetry)
        // link back to the request that produced the event.
        .AddSource("Npgsql")
        .AddSource("Marten")
        .AddSource("Wolverine")
        // #220 — drop redundant event-infrastructure spans (sub.* / handler.* / checkpoint*) so
        // traces stay readable; everything else keeps flowing, correlated by trace id.
        .SetSampler(new SubscriptionNoiseSampler(new AlwaysOnSampler())))
      // #305 — Marten + Wolverine also publish OTel meters (message throughput, handler timing, event
      // append/query counters). Collect them alongside the Azure Monitor distro's default runtime +
      // HTTP metrics so the Critter Stack's own health is visible in App Insights.
      .WithMetrics(metrics => metrics
        .AddMeter("Marten")
        .AddMeter("Wolverine"));

    // Domain-event logs (carrying the "EventType" attribute from GameTelemetryHandler) become
    // Application Insights customEvents — the mapping lives here in the host, so the app/core
    // keeps emitting plain ILogger events with no telemetry coupling.
    services.ConfigureOpenTelemetryLoggerProvider(logging => logging.AddProcessor(new DomainEventLogProcessor()));

    // #216 — tag request spans with session.id = game and enduser.id = player (synthetic, no PII)
    // from the route, so requests + their dependency spans flow into App Insights' Users/Sessions
    // experiences sliced by game and player. EnrichWithHttpResponse runs after routing, so the
    // {gameId}/{playerId} route values are populated.
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
