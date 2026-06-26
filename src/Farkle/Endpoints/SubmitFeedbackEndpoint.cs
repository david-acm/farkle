using FastEndpoints;
using Farkle.Application;
using Farkle.Domain.Feedback;
using Microsoft.Extensions.Logging;
using static Farkle.Contracts.HttpRequests;
using static Farkle.Contracts.HttpResponses;

namespace Farkle.Endpoints;

// #277 — anonymous feedback submission. Input validation lives here (not validation-as-events):
// without an aggregate there's no IErrorEvent path, so an empty / oversized message is a plain
// 400. On success the FeedbackSubmitted fact is appended to the "Feedback-{sessionId}" stream.
internal class SubmitFeedbackEndpoint(
  ILogger<SubmitFeedbackEndpoint> logger,
  IFeedbackWriter                 writer)
  : TypedEndpoint<SubmitFeedbackRequest, SubmitFeedbackResponse>
{
  private const int MaxMessageLength = 2000;

  public override void Configure()
  {
    Post("/api/feedback");
    AllowAnonymous();
  }

  public override async Task HandleAsync(SubmitFeedbackRequest req, CancellationToken ct)
  {
    if (string.IsNullOrWhiteSpace(req.SessionId))
      AddError(r => r.SessionId, "A feedback session id is required.");

    if (string.IsNullOrWhiteSpace(req.Message))
      AddError(r => r.Message, "Feedback message cannot be empty.");
    else if (req.Message.Length > MaxMessageLength)
      AddError(r => r.Message, $"Feedback message cannot exceed {MaxMessageLength} characters.");

    var sentiment = NormalizeSentiment(req.Sentiment);
    if (sentiment is null)
      AddError(r => r.Sentiment, "Sentiment must be 'Up' or 'Down'.");

    ThrowIfAnyErrors(); // FastEndpoints → 400 with the collected validation errors

    var @event = new FeedbackEvents.V1.FeedbackSubmitted(
      req.GameId, req.PlayerId, req.Stage, req.Message.Trim(), sentiment!, req.Route, DateTimeOffset.UtcNow);

    await writer.AppendAsync(req.SessionId, @event, ct);

    logger.LogInformation(
      "Feedback submitted for session {SessionId} (game {GameId}, sentiment {Sentiment})",
      req.SessionId, req.GameId, sentiment);

    await Send.OkAsync(new SubmitFeedbackResponse(req.SessionId), ct);
  }

  // Accept the words or the glyphs; normalise to the stored "Up"/"Down". Null → invalid (400).
  private static string? NormalizeSentiment(string? sentiment) =>
    sentiment?.Trim().ToLowerInvariant() switch
    {
      "up"   or "👍" => "Up",
      "down" or "👎" => "Down",
      _              => null,
    };
}
