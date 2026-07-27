using FluentAssertions;
using Moq;
using HotDice.Ui.Features;
using HotDice.Ui.Pages.Game.Components;
using HotDice.Ui.Services;
using static HotDice.Contracts.HttpResponses;

namespace HotDice.SpaTests.Handlers.PassTurn;

// H3 — covers HotDice.Ui/Features/Actions/PassTurn.cs.
public class HandleShould : HandlerTestContext
{
  [Fact]
  public async Task UpdateCurrentPlayer_ResetDice_AndPublishScoreboard()
  {
    // Pre-populate: we had dice in play and a turn score going.
    State.DiceInPlay.Add(DiceInfo.Unselected(0, DieValue.Five));
    State.DiceInPlay.Add(DiceInfo.Selected(1, DieValue.One));

    Mock.Get(GameService)
      .Setup(s => s.PassTurnAsync(It.IsAny<int>(), It.IsAny<int>()))
      .ReturnsAsync(new PassTurnResponse(
        GameId:          0,
        PlayerId:        1,
        NewScore:        150,
        Winner:          null,
        CurrentPlayerId: 2,
        Scoreboard: [new PlayerScore(1, "Alice", 150), new PlayerScore(2, "Bob", 0)]));

    await Sender.Send(new GameState.PassTurn.Action());

    State.CurrentPlayerId.Should().Be(2);
    State.Scoreboard.Should().HaveCount(2);
    State.Scoreboard.Single(s => s.Name == "Alice").Score.Should().Be(150);
    State.WinnerName.Should().BeNull();
    State.DiceInPlay.Should().BeEmpty("dice are reset between turns");
    State.TurnScore.Value.Should().Be(0);
  }

  [Fact]
  public async Task SurfaceWinnerName_WhenGameEnds()
  {
    Mock.Get(GameService)
      .Setup(s => s.PassTurnAsync(It.IsAny<int>(), It.IsAny<int>()))
      .ReturnsAsync(new PassTurnResponse(
        GameId:          0,
        PlayerId:        1,
        NewScore:        10_000,
        Winner:          new WinnerResponse(1, "Alice", 10_000),
        CurrentPlayerId: 1,
        Scoreboard:      [new PlayerScore(1, "Alice", 10_000)]));

    await Sender.Send(new GameState.PassTurn.Action());

    State.WinnerName.Should().Be("Alice");
    State.IsGameOver.Should().BeTrue();
  }
}
