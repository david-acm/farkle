using Farkle.ApiClient.Models;
using Farkle.Features;
using Farkle.WebTests.Harness;
using Microsoft.Kiota.Abstractions;

namespace Farkle.WebTests.Slices;

// PassTurn slice (POST .../turns): locks in the turn score, rotates to the next player, and broadcasts
// the turn change — the original real-time multiplayer trigger. The broadcast is asserted via
// TrackedSession in one session alongside the HTTP response (per #304).
[Collection(IntegrationCollection.Name)]
public class PassTurnShould(AppFixture fixture) : IntegrationTest(fixture)
{
  [Fact]
  public async Task RotateTheTurnToTheNextPlayer()
  {
    var (gameId, player1, player2) = await StartTwoPlayerGameAsync();

    await RollKeepScoringDiceAsync(gameId, player1);
    var pass = await PassAsync(gameId, player1);
    Assert.Equal(player2, pass!.CurrentPlayerId);

    // Player 2 can now roll — the turn rotated correctly.
    var roll2 = await RollAsync(gameId, player2);
    Assert.NotNull(roll2?.DiceValues);
    Assert.NotEmpty(roll2!.DiceValues!);
  }

  [Fact]
  public async Task LockTheTurnScoreIntoTheCumulativeScore()
  {
    var (gameId, player1, _) = await StartTwoPlayerGameAsync();

    var turnScore = await RollKeepScoringDiceAsync(gameId, player1);
    var pass = await PassAsync(gameId, player1);

    Assert.NotNull(pass);
    Assert.Equal(turnScore, pass!.NewScore);
  }

  [Fact]
  public async Task IncludeTheFullScoreboard()
  {
    var (gameId, player1, _) = await StartTwoPlayerGameAsync();

    await RollKeepScoringDiceAsync(gameId, player1);
    var pass = await PassAsync(gameId, player1);

    Assert.NotNull(pass?.Scoreboard);
    Assert.Equal(2, pass!.Scoreboard!.Count);
    var names = pass.Scoreboard.Select(p => p.Name).ToHashSet();
    Assert.Contains("David", names);
    Assert.Contains("Allison", names);
    var david = pass.Scoreboard.First(p => p.Name == "David");
    Assert.Equal(pass.NewScore, david.Score);
  }

  [Fact]
  public async Task RejectAPassBeforeRolling()
  {
    var (gameId, player1, _) = await StartTwoPlayerGameAsync();

    var ex = await Assert.ThrowsAsync<ApiException>(() => PassAsync(gameId, player1));
    Assert.Equal(400, ex.ResponseStatusCode);
  }

  [Fact]
  public async Task BroadcastTurnChangedAfterAPass()
  {
    var (gameId, player1, _) = await StartTwoPlayerGameAsync();
    await RollKeepScoringDiceAsync(gameId, player1);

    PassTurnResponse? response = null;
    var tracked = await TrackAsync(async () => response = await PassAsync(gameId, player1));

    // The HTTP response and the tracked broadcast, asserted in one session.
    Assert.NotNull(response);
    Assert.Equal(gameId, response!.GameId);
    Assert.Equal(2, response.CurrentPlayerId); // turn rotated to player 2
    Assert.Equal(2, response.TurnNumber);      // #244 — began on turn 1, one pass → turn 2

    var msg = tracked.Executed.SingleMessage<GameNotifications.TurnChanged>();
    Assert.Equal(gameId, msg.GameId);
    Assert.Equal(player1, msg.PlayerId);       // the passing player
  }

  // Rolls for player 1 and keeps every scoring die (1s and 5s), returning the resulting turn score.
  private async Task<int> RollKeepScoringDiceAsync(int gameId, int player1)
  {
    var roll = await RollAsync(gameId, player1);
    var scoringDice = (roll!.DiceValues ?? [])
      .Where(v => v == 1 || v == 5).Select(v => v!.Value).ToArray();

    if (scoringDice.Length == 0) return 0;

    var kept = await KeepAsync(gameId, player1, scoringDice);
    return kept!.TurnScore ?? 0;
  }
}
