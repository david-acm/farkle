using HotDice.Application;
using HotDice.Domain.Feedback;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Wolverine.Http;
using static HotDice.Contracts.HttpRequests;
using static HotDice.Contracts.HttpResponses;

namespace HotDice.Features.Feedback;

// SubmitFeedback slice endpoint (#277, #303). Anonymous. No aggregate, so input validity is guarded
// by SubmitFeedbackRequestValidator through the Wolverine.HTTP FluentValidation middleware (→ 400
// before this runs) rather than validation-as-events; on success the FeedbackSubmitted fact is
// appended to the "feedback-{sessionId}" Marten stream.
public static class SubmitFeedbackEndpoint
{
  internal const int MaxMessageLength = 2000;

  [AllowAnonymous]
  [WolverinePost("/api/feedback")]
  public static async Task<Ok<SubmitFeedbackResponse>> Post(
    SubmitFeedbackRequest body, IFeedbackWriter writer, CancellationToken ct)
  {
    // Shape is already validated (SessionId/Message/Sentiment) by the middleware, so the sentiment is
    // a recognised value here.
    var sentiment = NormalizeSentiment(body.Sentiment)!;

    var @event = new FeedbackEvents.V1.FeedbackSubmitted(
      body.GameId, body.PlayerId, body.Stage, body.Message.Trim(), sentiment, body.Route, DateTimeOffset.UtcNow);

    await writer.AppendAsync(body.SessionId, @event, ct);

    return TypedResults.Ok(new SubmitFeedbackResponse(body.SessionId));
  }

  // Accept the words or the glyphs; normalise to the stored "Up"/"Down". Null → invalid. Shared with
  // SubmitFeedbackRequestValidator so the accepted set has one source of truth.
  internal static string? NormalizeSentiment(string? sentiment) =>
    sentiment?.Trim().ToLowerInvariant() switch
    {
      "up"   or "👍" => "Up",
      "down" or "👎" => "Down",
      _              => null,
    };
}
