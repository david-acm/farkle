using Microsoft.Extensions.Logging;
using Moq;
using RichardSzalay.MockHttp;
using WebApp.Client.Services;
using static Farkle.Contracts.HttpResponses;
using static Farkle.SpaTests.Services.MockHttpClientBUnitHelpers;

namespace Farkle.SpaTests.Services;

public class CreateGameShould
{
  [Fact]
  public async Task PostBodylessAndReturnGeneratedIdAsync()
  {
    // Given — the server generates the id; the client POSTs no request body.
    var mock = GetMockHttpClient();
    mock.Expect(HttpMethod.Post, "http://localhost/api/games")
      .RespondJson(new StartGameResponse(4242));

    var sut = new GameService(mock.ToFarkleApiClient(), Mock.Of<ILogger<GameService>>());

    // When
    var id = await sut.CreateGameAsync();

    // Then
    id.Should().Be(4242);
    mock.VerifyNoOutstandingExpectation();
  }
}
