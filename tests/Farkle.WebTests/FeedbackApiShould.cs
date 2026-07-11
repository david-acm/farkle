using System;
using System.Linq;
using System.Threading.Tasks;
using Farkle.ApiClient;
using Farkle.ApiClient.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Kiota.Abstractions;
using Microsoft.Kiota.Abstractions.Authentication;
using Microsoft.Kiota.Http.HttpClientLibrary;

namespace Farkle.WebTests;

// #277 — POST /api/feedback is anonymous and appends a FeedbackSubmitted fact to the
// "Feedback-{sessionId}" stream, which the read-model subscription folds into FeedbackView.
// One happy path + one validation rejection + the projection (per the layered testing guide —
// business rules aren't re-proven here).
public class FeedbackApiShould : IClassFixture<GameApiWebAppFactory>
{
    private readonly GameApiWebAppFactory _factory;
    private readonly FarkleApiClient      _client;

    public FeedbackApiShould(GameApiWebAppFactory factory)
    {
        _factory = factory;
        // Feedback is anonymous — no login needed (unlike the game endpoints).
        var httpClient = new HttpClient(factory.Server.CreateHandler())
        {
            BaseAddress = factory.Server.BaseAddress
        };
        var adapter = new HttpClientRequestAdapter(new AnonymousAuthenticationProvider(), httpClient: httpClient);
        adapter.BaseUrl = httpClient.BaseAddress?.ToString().TrimEnd('/') ?? string.Empty;
        _client = new FarkleApiClient(adapter);
    }

    [Fact]
    public async Task AcceptAnonymousFeedbackAndEchoTheSessionAsync()
    {
        var sessionId = $"sess-{Guid.NewGuid():N}";

        var response = await _client.Api.Feedback.PostAsync(new SubmitFeedbackRequest
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
    public async Task RejectEmptyFeedbackMessageAsync()
    {
        // #303 — the endpoint now returns a typed ValidationProblem (400), which Kiota surfaces as
        // the strongly-typed HttpValidationProblemDetails error rather than a bare ApiException.
        var ex = await Assert.ThrowsAsync<HttpValidationProblemDetails>(() =>
            _client.Api.Feedback.PostAsync(new SubmitFeedbackRequest
            {
                SessionId = $"sess-{Guid.NewGuid():N}",
                Message   = "   ",
                Sentiment = "Down",
            }));

        Assert.Equal(400, ex.Status);
    }

    // The feedback triage read model (a $all EF projection) is retired at the Marten cutover
    // (ADR 0004) — the submission is appended to a Marten "feedback-{session}" stream; a triage
    // read model can return as a Marten projection in a follow-up. The write path is covered by
    // AcceptAnonymousFeedbackAndEchoTheSessionAsync above.
}
