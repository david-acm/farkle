namespace Farkle.Application;

// #277 — read-side persistence seam for feedback, kept EF-agnostic so Farkle.Application stays
// infrastructure-free (mirrors IGameViewStore). The projector hands it a plain record; the
// EF/Postgres implementation (Farkle.Infrastructure) maps it to a FeedbackView row. Keyed by the
// $all global position so a redelivery is idempotent.
public interface IFeedbackViewStore
{
  Task SaveAsync(FeedbackViewRecord record, CancellationToken ct);
}

public sealed record FeedbackViewRecord(
  long           Position,
  string         SessionId,
  int?           GameId,
  int?           PlayerId,
  string?        Stage,
  string         Message,
  string         Sentiment,
  string?        Route,
  DateTimeOffset CreatedAt);
