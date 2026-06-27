namespace Farkle.Infrastructure.ReadModel;

// #277 — read model for triage: one row per submitted feedback. Keyed by the $all global position
// (unique + monotonic) so reprocessing an event after a crash is idempotent. Indexed by GameId so
// feedback can be sliced per game. No PII (no player names) — synthetic ids only.
public class FeedbackView
{
    // The $all global position of the source event — also the projection key.
    public long Position { get; set; }

    // The feedback stream key ("Feedback-{SessionId}"), client-generated, persisted in localStorage.
    public string SessionId { get; set; } = string.Empty;

    public int? GameId { get; set; }

    public int? PlayerId { get; set; }

    public string? Stage { get; set; }

    public string Message { get; set; } = string.Empty;

    // "Up" | "Down".
    public string Sentiment { get; set; } = string.Empty;

    public string? Route { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
