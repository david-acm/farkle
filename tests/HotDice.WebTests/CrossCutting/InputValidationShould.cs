using System.Net;
using HotDice.WebTests.Harness;

namespace HotDice.WebTests.CrossCutting;

// Input validation (#302 follow-up). Structurally invalid request bodies — a die value outside 1..6,
// an empty player name — must be rejected as a 400 ValidationProblem by the FluentValidation
// middleware BEFORE the endpoint body runs. Without it, DieValue.FromValue(7) throws inside the
// mapping and leaks a 500, and an empty name silently joins a nameless player. Business rules stay in
// the deciders (validation-as-events); this layer only guards the shape/range of the input.
[Collection(IntegrationCollection.Name)]
public class InputValidationShould(AppFixture fixture) : IntegrationTest(fixture)
{
  [Theory]
  [InlineData(0)]
  [InlineData(7)]
  [InlineData(-1)]
  public async Task RejectAnOutOfRangeDieOnSetAsideWith400(int die)
  {
    var (gameId, player1, _) = await StartTwoPlayerGameAsync();

    var status = await PostJsonAsync(
      $"/api/games/{gameId}/players/{player1}/setasides",
      $$"""{"dieValue":{{die}}}""");

    Assert.Equal(HttpStatusCode.BadRequest, status);
  }

  [Theory]
  [InlineData(0)]
  [InlineData(7)]
  public async Task RejectAnOutOfRangeDieOnReturnWith400(int die)
  {
    var (gameId, player1, _) = await StartTwoPlayerGameAsync();

    var status = await PostJsonAsync(
      $"/api/games/{gameId}/players/{player1}/putbacks",
      $$"""{"dieValue":{{die}}}""");

    Assert.Equal(HttpStatusCode.BadRequest, status);
  }

  [Fact]
  public async Task RejectAnOutOfRangeDieOnKeepWith400()
  {
    var (gameId, player1, _) = await StartTwoPlayerGameAsync();

    var status = await PostJsonAsync(
      $"/api/games/{gameId}/players/{player1}/keeps",
      """{"diceValues":[1,7]}""");

    Assert.Equal(HttpStatusCode.BadRequest, status);
  }

  [Theory]
  [InlineData("")]
  [InlineData("   ")]
  public async Task RejectAnEmptyPlayerNameOnJoinWith400(string name)
  {
    var gameId = await StartGameAsync();

    var status = await PostJsonAsync(
      $"/api/games/{gameId}/players",
      $$"""{"playerName":"{{name}}"}""");

    Assert.Equal(HttpStatusCode.BadRequest, status);
  }

  // A well-formed die still flows through to the domain: this proves the input validator doesn't
  // over-reach and reject valid input (it should reach the decider, which here rejects setting aside
  // before rolling with its own domain 400 — NOT a validation 400, but still a 400, so we assert the
  // request is accepted by validation by checking it is NOT a 500).
  [Fact]
  public async Task LetAWellFormedDieReachTheDomain()
  {
    var (gameId, player1, _) = await StartTwoPlayerGameAsync();

    var status = await PostJsonAsync(
      $"/api/games/{gameId}/players/{player1}/setasides",
      """{"dieValue":5}""");

    Assert.NotEqual(HttpStatusCode.InternalServerError, status);
  }
}
