using System.Net;
using Farkle.WebTests.Harness;

namespace Farkle.WebTests.CrossCutting;

// Regression for #310 (was #237): concurrent play commands against the SAME game stream must not
// surface the raw optimistic-concurrency conflict (Marten ConcurrencyException → HTTP 412/500) to the
// client. Wolverine retries the losing request, which re-fetches the now-advanced stream, re-runs the
// pure decider, and either succeeds or returns a genuine domain error (here: RolledTwice → 400).
[Collection(IntegrationCollection.Name)]
public class ConcurrencyShould(AppFixture fixture) : IntegrationTest(fixture)
{
  [Fact]
  public async Task AbsorbConcurrentRollsOnTheSameStreamWithoutLeaking412Or500()
  {
    var (gameId, player1, _) = await StartTwoPlayerGameAsync();

    // A fresh turn: every one of these rolls is individually valid against the pre-roll state, so they
    // all FetchForWriting at the same stream version and race to append DiceRolled. Exactly one wins;
    // without a concurrency retry the losers surface Marten's ConcurrencyException as HTTP 412. With
    // the retry they re-fetch (stage is now Keeping) and get a clean RolledTwice → 400 instead.
    var rollUrl = $"/api/games/{gameId}/players/{player1}/rolls";
    var statuses = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => PostStatusAsync(rollUrl)));

    Assert.DoesNotContain(HttpStatusCode.PreconditionFailed, statuses);   // 412 — the leaked conflict
    Assert.DoesNotContain(HttpStatusCode.InternalServerError, statuses);  // 500
    Assert.Equal(1, statuses.Count(s => s == HttpStatusCode.OK));         // exactly one roll wins the race
    Assert.All(statuses, s => Assert.True(
      s is HttpStatusCode.OK or HttpStatusCode.BadRequest,
      $"expected 200 or a domain 400, got {(int)s} {s}"));
  }
}
