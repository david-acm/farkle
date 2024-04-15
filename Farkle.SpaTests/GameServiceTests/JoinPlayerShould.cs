using Castle.Core.Logging;
using Farkle.Contracts;
using Farkle.Spa.Services;
using Microsoft.Extensions.Logging;
using Moq;
using RichardSzalay.MockHttp;

namespace Farkle.SpaTests.GameServiceTests;

public class JoinPlayerShould
{
  [Fact]
  public async Task CallApiAsync()
  {
    // Given
    var mock = MockHttpClientBUnitHelpers.GetMockHttpClient();
    mock.Expect(HttpMethod.Post, "/api/games/1/players/1")
      .RespondJson(
        new HttpResponses.JoinPlayerResponse(1));

    var sut = new GameService(mock.ToHttpClient(), Mock.Of<ILogger<GameService>>());

    // When
    await sut.JoinPlayerAsync(1, 1, "David");

    // Then
    mock.VerifyNoOutstandingExpectation();
  }
}
