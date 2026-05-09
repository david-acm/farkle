using Microsoft.Extensions.Logging;
using Moq;
using RichardSzalay.MockHttp;
using WebApp.Client.Services;
using static Farkle.Contracts.HttpResponses;
using static Farkle.SpaTests.GameServiceTests.MockHttpClientBUnitHelpers;

namespace Farkle.SpaTests.GameServiceTests;

public class PassTurnShould
{
  [Fact]
  public async Task CallApiAsync()
  {
    // Given
    var mock = GetMockHttpClient();
    mock.Expect(HttpMethod.Post, "http://localhost/api/games/1/players/1/turns")
      .RespondJson(new PassTurnResponse(1, 1, 350, null));

    var sut = new GameService(mock.ToFarkleApiClient(), Mock.Of<ILogger<GameService>>());

    // When
    await sut.PassTurnAsync(1, 1);

    // Then
    mock.VerifyNoOutstandingExpectation();
  }

  [Fact]
  public async Task ReturnNewScoreAsync()
  {
    // Given
    var mock = GetMockHttpClient();
    mock.When("http://localhost/api/games/1/players/1/turns")
      .RespondJson(new PassTurnResponse(1, 1, 350, null));

    var sut = new GameService(mock.ToFarkleApiClient(), Mock.Of<ILogger<GameService>>());

    // When
    var result = await sut.PassTurnAsync(1, 1);

    // Then
    result.NewScore.Should().Be(350);
    result.Winner.Should().BeNull();
  }

  [Fact]
  public async Task ReturnWinnerWhenGameIsWonAsync()
  {
    // Given
    var mock = GetMockHttpClient();
    mock.When("http://localhost/api/games/1/players/1/turns")
      .RespondJson(new PassTurnResponse(1, 1, 10_000, new WinnerResponse(1, "Alice", 10_000)));

    var sut = new GameService(mock.ToFarkleApiClient(), Mock.Of<ILogger<GameService>>());

    // When
    var result = await sut.PassTurnAsync(1, 1);

    // Then
    result.Winner.Should().NotBeNull();
    result.Winner!.Name.Should().Be("Alice");
    result.Winner.Score.Should().Be(10_000);
  }
}
