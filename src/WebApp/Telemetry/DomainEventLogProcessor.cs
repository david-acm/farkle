using OpenTelemetry;
using OpenTelemetry.Logs;

namespace WebApp.Telemetry;

/// <summary>
/// #216 — enriches domain-event log records (emitted by <c>GameTelemetryHandler</c>, identified by
/// the <c>EventType</c> attribute) for Application Insights:
/// <list type="bullet">
///   <item>adds <c>microsoft.custom_event.name</c> so the record becomes a <c>customEvent</c>;</item>
///   <item>maps <c>GameId</c> → <c>session.id</c> and <c>PlayerId</c> → <c>enduser.id</c> (synthetic,
///   PII-free) so events flow into App Insights' Users/Sessions experiences sliced by game and player.</item>
/// </list>
/// Keeping the mapping in the host means the app/core emits plain
/// <see cref="Microsoft.Extensions.Logging.ILogger"/> events with no telemetry coupling.
/// </summary>
public sealed class DomainEventLogProcessor : BaseProcessor<LogRecord>
{
  private const string EventTypeAttribute = "EventType";
  private const string GameIdAttribute = "GameId";
  private const string PlayerIdAttribute = "PlayerId";
  private const string CustomEventNameAttribute = "microsoft.custom_event.name";
  // OTel semantic-convention attributes the Azure Monitor exporter maps to AI session_Id / user_Id.
  private const string SessionIdAttribute = "session.id";
  private const string EndUserIdAttribute = "enduser.id";

  public override void OnEnd(LogRecord record)
  {
    if (record.Attributes is null)
      return;

    string? eventType = null;
    object? gameId = null;
    object? playerId = null;
    var alreadyTagged = false;
    foreach (var attribute in record.Attributes)
    {
      switch (attribute.Key)
      {
        case EventTypeAttribute: eventType = attribute.Value?.ToString(); break;
        case GameIdAttribute: gameId = attribute.Value; break;
        case PlayerIdAttribute: playerId = attribute.Value; break;
        case CustomEventNameAttribute: alreadyTagged = true; break;
      }
    }

    if (eventType is null || alreadyTagged)
      return;

    var attributes = new List<KeyValuePair<string, object?>>(record.Attributes)
    {
      new(CustomEventNameAttribute, $"HotDice.{eventType}")
    };
    // game → session, (game,player) → user. Synthetic ids only — never a display name (no PII).
    if (gameId is not null)
      attributes.Add(new(SessionIdAttribute, $"g{gameId}"));
    if (gameId is not null && playerId is not null)
      attributes.Add(new(EndUserIdAttribute, $"g{gameId}-p{playerId}"));

    record.Attributes = attributes;
  }
}
