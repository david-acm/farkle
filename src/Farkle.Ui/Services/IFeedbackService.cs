namespace Farkle.Ui.Services;

// #277 — submits user feedback to POST /api/feedback (anonymous). The feedback session id (the
// Feedback-{sessionId} stream key) is generated once and persisted in localStorage, so a browser's
// submissions accumulate in one stream over time.
public interface IFeedbackService
{
  Task SubmitFeedbackAsync(string message, string sentiment, int? gameId, string? route);
}
