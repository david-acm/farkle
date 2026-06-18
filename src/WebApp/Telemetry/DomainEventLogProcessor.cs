using OpenTelemetry;
using OpenTelemetry.Logs;

namespace WebApp.Telemetry;

/// <summary>
/// #216 — enriches domain-event log records (emitted by <c>GameTelemetryHandler</c>, identified by
/// the <c>EventType</c> attribute) for Application Insights:
/// <list type="bullet">
/// <item>adds <c>microsoft.custom_event.name</c> so the record becomes a <c>customEvent</c>;</item>
/// <item>maps <c>GameId</c> → <c>session.id</c> and <c>PlayerId</c> → <c>enduser.id</c> (synthetic,
/// PII-free) so events are sliceable by game and player.</item>
/// </list>
/// Keeping the mapping in the host means the app/core emits plain
/// <see cref="Microsoft.Extensions.Logging.ILogger"/> events.
/// </summary>
public sealed class DomainEventLogProcessor : BaseProcessor<LogRecord>
{
  private const string EventTypeAttribute = "EventType";
  private const string GameIdAttribute = "GameId";
  private const string PlayerIdAttribute = "PlayerId";
  private const string CustomEventNameAttribute = "microsoft.custom_event.name";
  private const string SessionIdAttribute = "session.id";
  private const string EndUserIdAttribute = "enduser.id";

  public override void OnEnd(LogRecord record)
  {
    if (record.Attributes is null)
      return;

    string? eventType = null;
    object? gameId = null;
    object? playerId = null;
    var alreadyEnriched = false;
    foreach (var attribute in record.Attributes)
    {
      switch (attribute.Key)
      {
        case EventTypeAttribute: eventType = attribute.Value?.ToString(); break;
        case GameIdAttribute: gameId = attribute.Value; break;
        case PlayerIdAttribute: playerId = attribute.Value; break;
        case CustomEventNameAttribute: alreadyEnriched = true; break;
      }
    }

    if (eventType is null || alreadyEnriched)
      return;

    var attributes = new List<KeyValuePair<string, object?>>(record.Attributes)
    {
      new(CustomEventNameAttribute, $"Farkle.{eventType}")
    };
    if (gameId is not null)
      attributes.Add(new(SessionIdAttribute, $"g{gameId}"));
    if (gameId is not null && playerId is not null)
      attributes.Add(new(EndUserIdAttribute, $"g{gameId}-p{playerId}"));

    record.Attributes = attributes;
  }
}
