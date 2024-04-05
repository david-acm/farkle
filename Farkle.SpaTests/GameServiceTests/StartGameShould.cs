using System.Net.Http;
using System.Threading.Tasks;
using Greedy.Spa.Services;
using RichardSzalay.MockHttp;

namespace Greedy.SpaTests.GameServiceTests;

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
