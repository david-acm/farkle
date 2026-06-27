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

        var response = await _client.Api.Feedback.PostAsync(new FarkleContractsHttpRequests_SubmitFeedbackRequest
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
        var ex = await Assert.ThrowsAsync<ApiException>(() =>
            _client.Api.Feedback.PostAsync(new FarkleContractsHttpRequests_SubmitFeedbackRequest
            {
                SessionId = $"sess-{Guid.NewGuid():N}",
                Message   = "   ",
                Sentiment = "Down",
            }));

        Assert.Equal(400, ex.ResponseStatusCode);
    }

    [Fact]
    public async Task ProjectFeedbackIntoTheReadModelAsync()
    {
        var sessionId = $"sess-{Guid.NewGuid():N}";

        await _client.Api.Feedback.PostAsync(new FarkleContractsHttpRequests_SubmitFeedbackRequest
        {
            SessionId = sessionId,
            Message   = "Found a layout bug on mobile",
            Sentiment = "Down",
            GameId    = 4242,
        });

        // The projection is updated asynchronously by the $all subscription — poll the read model.
        Farkle.Infrastructure.ReadModel.FeedbackView? row = null;
        for (var attempt = 0; attempt < 50; attempt++)
        {
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider
                    .GetRequiredService<Farkle.Infrastructure.ReadModel.ReadModelDbContext>();
                row = await db.FeedbackViews.FirstOrDefaultAsync(f => f.SessionId == sessionId);
                if (row is not null) break;
            }
            await Task.Delay(100);
        }

        Assert.NotNull(row);
        Assert.Equal("Found a layout bug on mobile", row!.Message);
        Assert.Equal("Down", row.Sentiment);
        Assert.Equal(4242, row.GameId);
        Assert.True(row.Position > 0, "the folded $all position is recorded");
    }
}
