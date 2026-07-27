using FluentAssertions;
using Moq;
using HotDice.Ui.Features;
using HotDice.Ui.Services;

namespace HotDice.SpaTests.Handlers.CreateGame;

// Covers HotDice.Ui/Features/Actions/CreateGame.cs and SetGameId.cs.
public class HandleShould : HandlerTestContext
{
  [Fact]
  public async Task SeedGameId_FromServerGeneratedId()
  {
    Mock.Get(GameService)
      .Setup(s => s.CreateGameAsync())
      .ReturnsAsync(4242);

    await Sender.Send(new GameState.CreateGame.Action());

    State.GameId.Value.Should().Be(4242);
  }

  [Fact]
  public async Task SetGameId_SeedsTheIdWithoutCreating()
  {
    await Sender.Send(new GameState.SetGameId.Action(new GameId(77)));

    State.GameId.Value.Should().Be(77);
    Mock.Get(GameService).Verify(s => s.CreateGameAsync(), Times.Never);
  }
}
