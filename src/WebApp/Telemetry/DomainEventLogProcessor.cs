using OpenTelemetry;
using OpenTelemetry.Logs;

namespace WebApp.Telemetry;

/// <summary>
/// #216 — promotes domain-event log records to Application Insights <c>customEvents</c>. Records
/// emitted by <c>GameTelemetryHandler</c> carry an <c>EventType</c> attribute; this adds the
/// <c>microsoft.custom_event.name</c> attribute the Azure Monitor exporter recognises, turning the
/// log into a customEvent (everything else stays a trace). Keeping the mapping in the host means
/// the app/core emits plain <see cref="Microsoft.Extensions.Logging.ILogger"/> events.
/// </summary>
public sealed class DomainEventLogProcessor : BaseProcessor<LogRecord>
{
  private const string EventTypeAttribute = "EventType";
  private const string CustomEventNameAttribute = "microsoft.custom_event.name";

  public override void OnEnd(LogRecord record)
  {
    if (record.Attributes is null)
      return;

    string? eventType = null;
    var alreadyTagged = false;
    foreach (var attribute in record.Attributes)
    {
      if (attribute.Key == EventTypeAttribute)
        eventType = attribute.Value?.ToString();
      else if (attribute.Key == CustomEventNameAttribute)
        alreadyTagged = true;
    }

    if (eventType is null || alreadyTagged)
      return;

    record.Attributes = new List<KeyValuePair<string, object?>>(record.Attributes)
    {
      new(CustomEventNameAttribute, $"Farkle.{eventType}")
    };
  }
}
