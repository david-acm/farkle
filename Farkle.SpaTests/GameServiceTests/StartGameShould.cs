using Farkle.Spa.Services;
using RichardSzalay.MockHttp;

namespace Farkle.SpaTests.GameServiceTests;

public class StartGameShould
{
  [Fact]
  public async Task CallApiAsync()
  {
    // Given
    var mock = MockHttpClientBUnitHelpers.GetMockHttpClient();
    mock.Expect(HttpMethod.Post, "/games")
      .RespondJson(
        new CommandResponse(
          new State(new[] { new Die("1", 1) }),
          true));

    var sut = new GameService(mock.ToHttpClient());

    // When
    await sut.StartGameAsync(1);

    // Then
    mock.VerifyNoOutstandingExpectation();
  }
}
