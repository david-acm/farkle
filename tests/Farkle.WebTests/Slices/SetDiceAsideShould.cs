using Farkle.ApiClient.Models;
using Farkle.Features;
using Farkle.SharedKernel.Scoring;
using Farkle.WebTests.Harness;

namespace Farkle.WebTests.Slices;

// SetDiceAside slice (POST .../setasides, #159): moving a rolled die into the transient set-aside
// selection is a first-class command that broadcasts TableChanged so spectators see the live keep
// selection. The roll uses real randomness; a greedy MachinePlayer picks the best-scoring trick and
// the setup retries only on a true Farkle (nothing scores).
[Collection(IntegrationCollection.Name)]
public class SetDiceAsideShould(AppFixture fixture) : IntegrationTest(fixture)
{
  [Fact]
  public async Task RecordAndBroadcastTheSetAsideSelection()
  {
    var (gameId, player1, bestKeep) = await StartGameAndRollBestKeepAsync();

    SetAsideResponse? last = null;
    var tracked = await TrackAsync(async () =>
    {
      // One die per command (SetDiceAside takes a single die); the response accumulates the selection.
      foreach (var die in bestKeep)
        last = await Client.Api.Games[gameId].Players[player1].Setasides.PostAsync(
          new SetDiceAsideRequest { DieValue = die });
    });

    // Each set-aside cascades a TableChanged broadcast — assert they fired for this game.
    var broadcasts = tracked.Executed.MessagesOf<GameNotifications.TableChanged>().ToList();
    Assert.Equal(bestKeep.Count, broadcasts.Count);
    Assert.All(broadcasts, m => Assert.Equal(gameId, m.GameId));

    // The HTTP response carries the full selection (compare as a multiset).
    Assert.NotNull(last);
    Assert.Equal(bestKeep.OrderBy(d => d), last!.DiceSetAside!.Select(d => d!.Value).OrderBy(d => d));
  }

  // Starts a two-player game and rolls once, retrying until the roll has a scoring trick, then returns
  // the dice the greedy MachinePlayer would keep (the highest-scoring trick).
  private async Task<(int gameId, int player1, IReadOnlyList<int> bestKeep)> StartGameAndRollBestKeepAsync()
  {
    for (var attempt = 0; attempt < 10; attempt++)
    {
      var (gameId, player1, _) = await StartTwoPlayerGameAsync();

      var roll = await RollAsync(gameId, player1);
      var dice = (roll!.DiceValues ?? []).Select(v => v ?? 0).ToList();
      var bestKeep = MachinePlayer.ChooseBestKeep(dice);
      if (bestKeep.Count > 0) return (gameId, player1, bestKeep);

      await Fixture.ResetAsync(); // clear the Farkle attempt's streams before retrying
    }

    throw new InvalidOperationException("No scoring trick was rolled after 10 attempts.");
  }
}
