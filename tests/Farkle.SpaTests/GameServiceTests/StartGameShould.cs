using Microsoft.Extensions.Logging;
using Moq;
using RichardSzalay.MockHttp;
using WebApp.Client.Services;
using static Farkle.Contracts.HttpResponses;
using static Farkle.SpaTests.GameServiceTests.MockHttpClientBUnitHelpers;

namespace Farkle.SpaTests.GameServiceTests;

public class StartGameShould
{
  [Fact]
  public async Task CallApiAsync()
  {
    // Given
    var mock = GetMockHttpClient();
    mock.Expect(HttpMethod.Post, "http://localhost/api/games")
      .RespondJson(new StartGameResponse(1));

    var sut = new GameService(mock.ToFarkleApiClient(), Mock.Of<ILogger<GameService>>());

    // When
    await sut.StartGameAsync(1);

    // Then
    mock.VerifyNoOutstandingExpectation();
  }
}
