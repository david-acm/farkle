using Microsoft.Extensions.Logging;
using Moq;
using RichardSzalay.MockHttp;
using WebApp.Client.Pages.Game.Components;
using WebApp.Client.Services;
using static Farkle.Contracts.HttpResponses;
using static Farkle.SpaTests.Services.MockHttpClientBUnitHelpers;

namespace Farkle.SpaTests.Services;

public class RollDiceShould
{
  [Fact]
  public async Task ReturnDiceValuesAsync()
  {
    // Given
    var mock = GetMockHttpClient();
    mock.When("http://localhost/api/games/1/players/1/rolls")
      .RespondJson(new RollDiceResponse(1, [1]));

    var sut = new GameService(mock.ToFarkleApiClient(), Mock.Of<ILogger<GameService>>());

    // When
    var dice = await sut.RollDiceAsync(1, 1);

    // Then
    dice.Value.Should().Contain(DieValue.One);
  }
}
