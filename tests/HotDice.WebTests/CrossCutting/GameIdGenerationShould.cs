using HotDice.WebTests.Harness;

namespace HotDice.WebTests.CrossCutting;

// StartGame collision-retry (ADR 0004). GameCreator draws a random game id and retries when the
// Marten stream already exists. Driven by the scripted-id host, whose IGameIdGenerator yields
// [424242, 424242, 424243]: the second create collides on 424242 and must land on 424243. Its own
// Alba host + Marten schema keep the fixed ids from colliding with the main integration host's data.
[Collection(ScriptedIdCollection.Name)]
public class GameIdGenerationShould(ScriptedIdAppFixture fixture) : IntegrationTest(fixture)
{
  [Fact]
  public async Task RetryWhenTheGeneratedIdCollides()
  {
    var first = await Client.Api.Games.PostAsync();
    Assert.Equal(424242, first!.Id);

    var second = await Client.Api.Games.PostAsync();
    Assert.Equal(424243, second!.Id);
  }
}
