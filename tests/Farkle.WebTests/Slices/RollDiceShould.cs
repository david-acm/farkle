using Farkle.Features;
using Farkle.WebTests.Harness;
using Microsoft.Kiota.Abstractions;

namespace Farkle.WebTests.Slices;

// RollDice slice (POST .../rolls): the in-turn player rolls the table centre and the endpoint
// broadcasts DiceRolled. Happy path + two representative error paths (out of turn, double roll) +
// the broadcast via TrackedSession.
[Collection(IntegrationCollection.Name)]
public class RollDiceShould(AppFixture fixture) : IntegrationTest(fixture)
{
  [Fact]
  public async Task PutSixDiceInTheTableCentre()
  {
    var (gameId, player1, _) = await StartTwoPlayerGameAsync();

    var roll = await RollAsync(gameId, player1);

    Assert.NotNull(roll?.DiceValues);
    Assert.Equal(6, roll!.DiceValues!.Count);
    Assert.All(roll.DiceValues, v => Assert.InRange(v!.Value, 1, 6));
  }

  [Fact]
  public async Task RejectARollFromThePlayerNotInTurn()
  {
    var (gameId, _, player2) = await StartTwoPlayerGameAsync();

    // Player 1 goes first; player 2 rolling out of turn must be rejected.
    var ex = await Assert.ThrowsAsync<ApiException>(() => RollAsync(gameId, player2));
    Assert.Equal(400, ex.ResponseStatusCode);
  }

  [Fact]
  public async Task RejectASecondRollBeforeKeeping()
  {
    var (gameId, player1, _) = await StartTwoPlayerGameAsync();

    await RollAsync(gameId, player1);           // first roll always succeeds
    var ex = await Assert.ThrowsAsync<ApiException>(() => RollAsync(gameId, player1));
    Assert.Equal(400, ex.ResponseStatusCode);
  }

  [Fact]
  public async Task BroadcastDiceRolledWhenTheInTurnPlayerRolls()
  {
    var (gameId, player1, _) = await StartTwoPlayerGameAsync();

    var tracked = await TrackAsync(() => RollAsync(gameId, player1));

    var msg = tracked.Executed.SingleMessage<GameNotifications.DiceRolled>();
    Assert.Equal(gameId, msg.GameId);
  }
}
