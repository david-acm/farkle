using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Serilog.Events;
using Serilog.Sinks.ApplicationInsights.TelemetryConverters;

namespace WebApp.Telemetry;

/// <summary>
/// #33 — routes Serilog log events to Application Insights by kind:
/// domain-event logs (emitted by <c>GameTelemetryHandler</c>, identified by the
/// <c>EventType</c> structured property) become <see cref="EventTelemetry"/> so they land in
/// the <c>customEvents</c> table and are queryable as events; every other log stays a trace.
/// Keeping this on the sink side lets the app/core emit plain <c>ILogger</c> events with no
/// Application Insights coupling.
/// </summary>
public sealed class DomainEventTelemetryConverter : TraceTelemetryConverter
{
  public override IEnumerable<ITelemetry> Convert(LogEvent logEvent, IFormatProvider formatProvider)
  {
    if (logEvent.Properties.TryGetValue("EventType", out var eventType))
    {
      var name = eventType is ScalarValue { Value: { } value } ? value.ToString() : "GameEvent";
      var telemetry = new EventTelemetry($"Farkle.{name}");
      // Copies the structured properties (GameId, Position, the destructured GameEvent, etc.)
      // onto the custom event so they're queryable in Azure Monitor.
      ForwardPropertiesToTelemetryProperties(logEvent, telemetry, formatProvider);
      yield return telemetry;
    }
    else
    {
      foreach (var telemetry in base.Convert(logEvent, formatProvider))
        yield return telemetry;
    }
  }
}
