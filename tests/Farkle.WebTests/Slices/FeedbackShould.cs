using Farkle.ApiClient;
using Farkle.ApiClient.Models;
using Farkle.WebTests.Harness;

namespace Farkle.WebTests.Slices;

// Feedback slice (POST /api/feedback, #277): anonymous, appends a FeedbackSubmitted fact to the
// "feedback-{session}" stream. One happy path + one validation rejection (business rules aren't
// re-proven here, per the layered testing guide). The endpoint is anonymous, so an authenticated
// client is accepted just the same.
[Collection(IntegrationCollection.Name)]
public class FeedbackShould(AppFixture fixture) : IntegrationTest(fixture)
{
  [Fact]
  public async Task AcceptAnonymousFeedbackAndEchoTheSession()
  {
    var sessionId = $"sess-{Guid.NewGuid():N}";

    var response = await Client.Api.Feedback.PostAsync(new SubmitFeedbackRequest
    {
      SessionId = sessionId,
      Message   = "The dice animation is delightful",
      Sentiment = "Up",
      Route     = "/",
    });

    Assert.NotNull(response);
    Assert.Equal(sessionId, response!.SessionId);
  }

  [Fact]
  public async Task RejectAnEmptyMessage()
  {
    // #303 — the endpoint returns a typed ValidationProblem (400), which Kiota surfaces as the
    // strongly-typed HttpValidationProblemDetails rather than a bare ApiException.
    var ex = await Assert.ThrowsAsync<HttpValidationProblemDetails>(() =>
      Client.Api.Feedback.PostAsync(new SubmitFeedbackRequest
      {
        SessionId = $"sess-{Guid.NewGuid():N}",
        Message   = "   ",
        Sentiment = "Down",
      }));

    Assert.Equal(400, ex.Status);
  }
}
