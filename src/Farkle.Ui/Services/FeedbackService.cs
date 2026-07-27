using Microsoft.JSInterop;
using Farkle.ApiClient;
using KiotaFeedbackRequest = Farkle.ApiClient.Models.SubmitFeedbackRequest;

namespace Farkle.Ui.Services;

// #277 — adapter over the generated Kiota client for the feedback endpoint. Owns the per-browser
// feedback session id (localStorage), so the caller only supplies the message + context.
public sealed class FeedbackService(FarkleApiClient client, IJSRuntime js, ILogger<FeedbackService> logger)
  : IFeedbackService
{
  private const string SessionStorageKey = "farkle:feedbackSessionId";

  public async Task SubmitFeedbackAsync(string message, string sentiment, int? gameId, string? route)
  {
    var sessionId = await GetOrCreateSessionIdAsync();

    await client.Api.Feedback.PostAsync(new KiotaFeedbackRequest
    {
      SessionId = sessionId,
      Message   = message,
      Sentiment = sentiment,
      GameId    = gameId,
      Route     = route,
    });

    logger.LogDebug("Feedback submitted (session {SessionId}, game {GameId})", sessionId, gameId);
  }

  // One stream per browser: reuse the stored id, or mint + persist a new one (dashless so the
  // "Feedback-{id}" stream key stays simple).
  private async Task<string> GetOrCreateSessionIdAsync()
  {
    var existing = await js.InvokeAsync<string?>("localStorage.getItem", SessionStorageKey);
    if (!string.IsNullOrEmpty(existing)) return existing;

    var id = Guid.NewGuid().ToString("N");
    await js.InvokeVoidAsync("localStorage.setItem", SessionStorageKey, id);
    return id;
  }
}
