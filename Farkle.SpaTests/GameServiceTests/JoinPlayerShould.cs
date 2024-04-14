using Castle.Core.Logging;
using Farkle.Spa.Services;
using Microsoft.Extensions.Logging;
using Moq;
using RichardSzalay.MockHttp;

namespace Farkle.SpaTests.GameServiceTests;

public class JoinPlayerShould
{
  [Fact]
  public async Task CallApiAsync()
  {
    // Given
    var mock = MockHttpClientBUnitHelpers.GetMockHttpClient();
    mock.Expect(HttpMethod.Post, "/players")
      .RespondJson(
        new CommandResponse(
          new State(new[] { new Die("1", 1) }),
          true));

    var sut = new GameService(mock.ToHttpClient(), Mock.Of<ILogger<GameService>>());

    // When
    await sut.JoinPlayerAsync(1, 1, "David");

    // Then
    mock.VerifyNoOutstandingExpectation();
  }
}
