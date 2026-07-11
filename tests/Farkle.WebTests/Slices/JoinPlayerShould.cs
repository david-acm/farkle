using Farkle.Features;
using Farkle.WebTests.Harness;

namespace Farkle.WebTests.Slices;

// JoinPlayer slice (POST /api/games/{id}/players): adds a player to the lobby and broadcasts the
// roster change. The broadcast half is asserted via TrackedSession — the endpoint cascades a
// LobbyChanged notification through the Marten outbox, and ExecuteAndWaitAsync waits for the
// GameBroadcastHandler to process it, no sleep.
[Collection(IntegrationCollection.Name)]
public class JoinPlayerShould(AppFixture fixture) : IntegrationTest(fixture)
{
  [Fact]
  public async Task ReturnTheLobbyRoster()
  {
    var gameId = await StartGameAsync();
    await JoinAsync(gameId, "David");
    var second = await Client.Api.Games[gameId].Players.PostAsync(
      new ApiClient.Models.JoinPlayerRequest { PlayerName = "Allison" });

    Assert.NotNull(second);
    Assert.Equal("WaitingForPlayers", second!.Stage);
    Assert.Equal(1, second.HostPlayerId);
    Assert.NotNull(second.Roster);
    Assert.Equal(2, second.Roster!.Count);
    Assert.Contains(second.Roster, p => p.Name == "David");
    Assert.Contains(second.Roster, p => p.Name == "Allison");
  }

  [Fact]
  public async Task BroadcastLobbyChangedWhenAPlayerJoins()
  {
    var gameId = await StartGameAsync();

    var tracked = await TrackAsync(() => JoinAsync(gameId, "David"));

    var msg = tracked.Executed.SingleMessage<GameNotifications.LobbyChanged>();
    Assert.Equal(gameId, msg.GameId);
  }
}
