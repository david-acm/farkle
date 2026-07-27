using HotDice.WebTests.Harness;
using Microsoft.Kiota.Abstractions;

namespace HotDice.WebTests.Slices;

// GetGame slice (GET /api/games/{id}): reads the Inline Marten GameState snapshot directly, so it's
// read-your-own-writes — no async projection to poll (ADR 0004). A read-only slice with no broadcast,
// so a plain Kiota round-trip.
[Collection(IntegrationCollection.Name)]
public class GetGameShould(AppFixture fixture) : IntegrationTest(fixture)
{
  [Fact]
  public async Task ReturnTheSnapshotForARefresh()
  {
    var (gameId, player1, _) = await StartTwoPlayerGameAsync();
    await RollAsync(gameId, player1); // now mid-turn: stage Keeping, dice on the table

    var snapshot = await Client.Api.Games[gameId].GetAsync();

    Assert.NotNull(snapshot);
    Assert.Equal(gameId, snapshot!.GameId);
    Assert.Equal("Keeping", snapshot.Stage);
    Assert.Equal(player1, snapshot.CurrentPlayerId);
    Assert.Equal(1, snapshot.HostPlayerId);
    Assert.Null(snapshot.Winner);

    // The full scoreboard (names + scores) is restored, not just the lobby roster.
    Assert.NotNull(snapshot.Scoreboard);
    Assert.Equal(2, snapshot.Scoreboard!.Count);
    Assert.Contains(snapshot.Scoreboard, p => p.Name == "David");
    Assert.Contains(snapshot.Scoreboard, p => p.Name == "Allison");

    // The dice the active player just rolled are part of the snapshot.
    Assert.NotNull(snapshot.TableCenter);
    Assert.NotEmpty(snapshot.TableCenter!);
    Assert.All(snapshot.TableCenter!, v => Assert.InRange(v!.Value, 1, 6));
  }

  [Fact]
  public async Task ReturnNotFoundForAnUnknownGame()
  {
    var ex = await Assert.ThrowsAsync<ApiException>(() => Client.Api.Games[999_999].GetAsync());
    Assert.Equal(404, ex.ResponseStatusCode);
  }
}
