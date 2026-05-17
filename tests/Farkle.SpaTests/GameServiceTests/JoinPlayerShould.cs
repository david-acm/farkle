using Microsoft.Extensions.Logging;
using Moq;
using RichardSzalay.MockHttp;
using WebApp.Client.Services;
using static Farkle.Contracts.HttpResponses;
using static Farkle.SpaTests.GameServiceTests.MockHttpClientBUnitHelpers;

namespace Farkle.SpaTests.GameServiceTests;

public class JoinPlayerShould
{
  [Fact]
  public async Task CallApiAsync()
  {
    // Given
    var mock = GetMockHttpClient();
    mock.Expect(HttpMethod.Post, "http://localhost/api/games/1/players")
      .RespondJson(new JoinPlayerResponse(1, 1));

    var sut = new GameService(mock.ToFarkleApiClient(), Mock.Of<ILogger<GameService>>());

    // When
    var result = await sut.JoinPlayerAsync(1, "David");

    // Then (server assigns ID and reports who is currently in turn)
    Assert.Equal(1, result.Id);
    Assert.Equal(1, result.CurrentPlayerId);
    mock.VerifyNoOutstandingExpectation();
  }
}

public class KeepDiceShould
{
  [Fact]
  public async Task CallApiAsync()
  {
    // Given
    var mock = GetMockHttpClient();
    mock.Expect(HttpMethod.Post, "http://localhost/api/games/1/players/1/keeps")
      .RespondJson(new KeepDiceResponse(1, 100));

    var sut = new GameService(mock.ToFarkleApiClient(), Mock.Of<ILogger<GameService>>());

    // When
    await sut.KeepDiceAsync(1, 1, [1]);

    // Then
    mock.VerifyNoOutstandingExpectation();
  }
}
