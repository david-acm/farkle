using Farkle.Application;
using Farkle.Domain.Feedback;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Wolverine.Http;
using static Farkle.Contracts.HttpRequests;
using static Farkle.Contracts.HttpResponses;

namespace Farkle.Features.Feedback;

// SubmitFeedback slice endpoint (#277, #303). Anonymous. No aggregate, so input validation is plain
// (empty/oversized message → 400) rather than validation-as-events; on success the FeedbackSubmitted
// fact is appended to the "feedback-{sessionId}" Marten stream.
public static class SubmitFeedbackEndpoint
{
  private const int MaxMessageLength = 2000;

  [AllowAnonymous]
  [WolverinePost("/api/feedback")]
  public static async Task<Results<Ok<SubmitFeedbackResponse>, ValidationProblem>> Post(
    SubmitFeedbackRequest body, IFeedbackWriter writer, CancellationToken ct)
  {
    var errors = new Dictionary<string, string[]>();

    if (string.IsNullOrWhiteSpace(body.SessionId))
      errors["SessionId"] = ["A feedback session id is required."];

    if (string.IsNullOrWhiteSpace(body.Message))
      errors["Message"] = ["Feedback message cannot be empty."];
    else if (body.Message.Length > MaxMessageLength)
      errors["Message"] = [$"Feedback message cannot exceed {MaxMessageLength} characters."];

    var sentiment = NormalizeSentiment(body.Sentiment);
    if (sentiment is null)
      errors["Sentiment"] = ["Sentiment must be 'Up' or 'Down'."];

    if (errors.Count > 0)
      return TypedResults.ValidationProblem(errors);

    var @event = new FeedbackEvents.V1.FeedbackSubmitted(
      body.GameId, body.PlayerId, body.Stage, body.Message.Trim(), sentiment!, body.Route, DateTimeOffset.UtcNow);

    await writer.AppendAsync(body.SessionId, @event, ct);

    return TypedResults.Ok(new SubmitFeedbackResponse(body.SessionId));
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
