using System.Net;
using Microsoft.Extensions.Logging;
using Moq;
using RichardSzalay.MockHttp;
using WebApp.Client.Services;
using static Farkle.Contracts.HttpResponses;
using static Farkle.SpaTests.Services.MockHttpClientBUnitHelpers;

namespace Farkle.SpaTests.Services;

public class GetGameStateShould
{
  [Fact]
  public async Task MapSnapshotFromApiAsync()
  {
    // Given
    var mock = GetMockHttpClient();
    mock.Expect(HttpMethod.Get, "http://localhost/api/games/1")
      .RespondJson(new GameStateResponse(
        GameId:          1,
        Stage:           "Keeping",
        CurrentPlayerId: 2,
        HostPlayerId:    1,
        TurnScore:       150,
        Scoreboard:      [new PlayerScore(1, "Alice", 0), new PlayerScore(2, "Bob", 150)],
        Winner:          null,
        TableCenter:     [1, 2, 3],
        DiceKept:        [5]));

    var sut = new GameService(mock.ToFarkleApiClient(), Mock.Of<ILogger<GameService>>());

    // When
    var result = await sut.GetGameStateAsync(1);

    // Then
    Assert.NotNull(result);
    Assert.Equal(1, result!.GameId);
    Assert.Equal("Keeping", result.Stage);
    Assert.Equal(2, result.CurrentPlayerId);
    Assert.Equal(150, result.TurnScore);
    Assert.Equal(2, result.Scoreboard.Count);
    Assert.Equal([1, 2, 3], result.TableCenter);
    Assert.Equal([5], result.DiceKept);
    mock.VerifyNoOutstandingExpectation();
  }

  [Fact]
  public async Task ReturnNullWhenGameNotFoundAsync()
  {
    // Given
    var mock = GetMockHttpClient();
    mock.When(HttpMethod.Get, "http://localhost/api/games/2")
      .Respond(HttpStatusCode.NotFound);

    var sut = new GameService(mock.ToFarkleApiClient(), Mock.Of<ILogger<GameService>>());

    // When
    var result = await sut.GetGameStateAsync(2);

    // Then — a 404 is "no such game", surfaced as null (not an exception).
    Assert.Null(result);
  }
}
