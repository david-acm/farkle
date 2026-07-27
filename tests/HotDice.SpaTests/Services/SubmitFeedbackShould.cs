using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Moq;
using RichardSzalay.MockHttp;
using HotDice.Ui.Services;
using static HotDice.SpaTests.Services.MockHttpClientBUnitHelpers;

namespace HotDice.SpaTests.Services;

public class SubmitFeedbackShould
{
  [Fact]
  public async Task ReuseTheStoredSessionIdAcrossSubmissions()
  {
    // The session id groups a browser's feedback into one stream. It is persisted under a
    // localStorage key that deliberately still reads "farkle:" — renaming it would orphan every
    // existing user's stream, which is a migration, not a rename.
    var mock = GetMockHttpClient();
    mock.Expect(HttpMethod.Post, "http://localhost/api/feedback").RespondJson(new { });

    var js = new Mock<IJSRuntime>();
    js.Setup(r => r.InvokeAsync<string?>("localStorage.getItem", It.IsAny<object?[]>()))
      .ReturnsAsync("existing-session");

    var sut = new FeedbackService(mock.ToHotDiceApiClient(), js.Object, Mock.Of<ILogger<FeedbackService>>());

    await sut.SubmitFeedbackAsync("it rolls badly", "down", gameId: 4242, route: "/game/4242");

    mock.VerifyNoOutstandingExpectation();
    // Nothing was minted, so nothing was written back.
    js.Verify(r => r.InvokeAsync<object>("localStorage.setItem", It.IsAny<object?[]>()), Times.Never);
  }

  [Fact]
  public async Task MintAndPersistASessionIdOnFirstUse()
  {
    var mock = GetMockHttpClient();
    mock.Expect(HttpMethod.Post, "http://localhost/api/feedback").RespondJson(new { });

    var js = new Mock<IJSRuntime>();
    js.Setup(r => r.InvokeAsync<string?>("localStorage.getItem", It.IsAny<object?[]>()))
      .ReturnsAsync(() => null);

    var sut = new FeedbackService(mock.ToHotDiceApiClient(), js.Object, Mock.Of<ILogger<FeedbackService>>());

    await sut.SubmitFeedbackAsync("nice dice", "up", gameId: null, route: "/");

    js.Verify(r => r.InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(
        "localStorage.setItem", It.IsAny<object?[]>()), Times.Once);
    mock.VerifyNoOutstandingExpectation();
  }
}
