using Farkle.ApiClient;
using Farkle.WebTests.Harness;
using Microsoft.Kiota.Abstractions;

namespace Farkle.WebTests.Slices;

// StartGame slice (POST /api/games): the server allocates a fresh game id. No broadcast — nobody is
// in the game yet — so a plain Kiota round-trip, no TrackedSession.
[Collection(IntegrationCollection.Name)]
public class StartGameShould(AppFixture fixture) : IntegrationTest(fixture)
{
  [Fact]
  public async Task ReturnAFreshPositiveId()
  {
    var id = await StartGameAsync();
    Assert.True(id > 0, "the server should generate a positive game id");
  }

  [Fact]
  public async Task ReturnDistinctIdsForTwoCreates()
  {
    var first  = await StartGameAsync();
    var second = await StartGameAsync();
    Assert.NotEqual(first, second);
  }

  [Fact]
  public async Task RejectUnauthenticatedRequests()
  {
    await AsAnonymous(async () =>
    {
      var ex = await Assert.ThrowsAsync<ApiException>(() => Client.Api.Games.PostAsync());
      Assert.Equal(401, ex.ResponseStatusCode);
    });
  }
}
