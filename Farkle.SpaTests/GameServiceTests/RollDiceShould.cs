using Ardalis.Result;
using Farkle.Spa.Components;
using Farkle.Spa.Services;
using Microsoft.Extensions.Logging;
using Moq;
using RichardSzalay.MockHttp;
using static Farkle.Contracts.HttpResponses;
using static Farkle.SpaTests.GameServiceTests.MockHttpClientBUnitHelpers;
using Services_Die=Farkle.Spa.Services.Die;

namespace Farkle.SpaTests.GameServiceTests;

public class RollDiceShould
{
  [Fact]
  public async Task ReturnDiceValuesAsync()
  {
    // Given
    var mock = GetMockHttpClient();
    mock.When("http://localhost:8000/api/games/1/players/1/rolls")
      .RespondJson(
        new RollDiceResponse(1, [1]));

    var sut = new GameService(mock.ToHttpClient(), Mock.Of<ILogger<GameService>>());

    // When
    var dice = await sut.RollDiceAsync(1, 1);

    // Then
    dice.Value.Should().Contain(DiceValue.One);
  }
}
