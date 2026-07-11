using Farkle.ApiClient;
using Farkle.Features;
using Farkle.WebTests.Harness;
using Microsoft.Kiota.Abstractions;

namespace Farkle.WebTests.Slices;

// BeginGame slice (POST /api/games/{id}/start): the host moves the lobby into play and the endpoint
// broadcasts GameBegan. One happy path, one representative error (non-host), and the broadcast via
// TrackedSession.
[Collection(IntegrationCollection.Name)]
public class BeginGameShould(AppFixture fixture) : IntegrationTest(fixture)
{
  [Fact]
  public async Task LetTheHostBeginPlay()
  {
    var gameId = await StartGameAsync();
    await JoinAsync(gameId, "David");
    await JoinAsync(gameId, "Allison");

    var lobby = await BeginAsync(gameId);

    Assert.NotNull(lobby);
    Assert.Equal("Rolling", lobby!.Stage);
    Assert.Equal(2, lobby.Roster!.Count);
  }

  [Fact]
  public async Task RejectBeginFromANonHost()
  {
    var gameId = await StartGameAsync();
    await JoinAsync(gameId, "David");
    await JoinAsync(gameId, "Allison");

    // Only player 1 (the creator) may begin play; player 2 must be rejected.
    var ex = await Assert.ThrowsAsync<ApiException>(() => BeginAsync(gameId, hostId: 2));
    Assert.Equal(400, ex.ResponseStatusCode);
  }

  [Fact]
  public async Task BroadcastGameBeganWhenTheHostStarts()
  {
    var gameId = await StartGameAsync();
    await JoinAsync(gameId, "David");
    await JoinAsync(gameId, "Allison");

    var tracked = await TrackAsync(() => BeginAsync(gameId));

    var msg = tracked.Executed.SingleMessage<GameNotifications.GameBegan>();
    Assert.Equal(gameId, msg.GameId);
  }
}
