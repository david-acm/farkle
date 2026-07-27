using Microsoft.Extensions.Logging;
using Moq;
using RichardSzalay.MockHttp;
using HotDice.Ui.Services;
using static HotDice.Contracts.HttpResponses;
using static HotDice.SpaTests.Services.MockHttpClientBUnitHelpers;

namespace HotDice.SpaTests.Services;

public class CreateGameShould
{
  [Fact]
  public async Task PostBodylessAndReturnGeneratedIdAsync()
  {
    // Given — the server generates the id; the client POSTs no request body.
    var mock = GetMockHttpClient();
    mock.Expect(HttpMethod.Post, "http://localhost/api/games")
      .RespondJson(new StartGameResponse(4242));

    var sut = new GameService(mock.ToHotDiceApiClient(), Mock.Of<ILogger<GameService>>());

    // When
    var id = await sut.CreateGameAsync();

    // Then
    id.Should().Be(4242);
    mock.VerifyNoOutstandingExpectation();
  }
}
