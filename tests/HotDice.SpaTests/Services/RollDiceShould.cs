using Microsoft.Extensions.Logging;
using Moq;
using RichardSzalay.MockHttp;
using HotDice.Ui.Pages.Game.Components;
using HotDice.Ui.Services;
using static HotDice.Contracts.HttpResponses;
using static HotDice.SpaTests.Services.MockHttpClientBUnitHelpers;

namespace HotDice.SpaTests.Services;

public class RollDiceShould
{
  [Fact]
  public async Task ReturnDiceValuesAsync()
  {
    // Given
    var mock = GetMockHttpClient();
    mock.When("http://localhost/api/games/1/players/1/rolls")
      .RespondJson(new RollDiceResponse(1, [1]));

    var sut = new GameService(mock.ToHotDiceApiClient(), Mock.Of<ILogger<GameService>>());

    // When
    var dice = await sut.RollDiceAsync(1, 1);

    // Then
    dice.Value.Should().Contain(DieValue.One);
  }
}
