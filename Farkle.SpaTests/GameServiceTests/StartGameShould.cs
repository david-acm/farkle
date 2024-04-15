using Farkle.Spa.Services;
using Microsoft.Extensions.Logging;
using Moq;
using RichardSzalay.MockHttp;
using static Farkle.Contracts.HttpResponses;

namespace Farkle.SpaTests.GameServiceTests;

public class StartGameShould
{
  [Fact]
  public async Task CallApiAsync()
  {
    // Given
    var mock = MockHttpClientBUnitHelpers.GetMockHttpClient();
    mock.Expect(HttpMethod.Post, "/api/games")
      .RespondJson(new StartGameResponse(1));

    var sut = new GameService(mock.ToHttpClient(), Mock.Of<ILogger<GameService>>());

    // When
    await sut.StartGameAsync(1);

    // Then
    mock.VerifyNoOutstandingExpectation();
  }
}
