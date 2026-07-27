using FluentAssertions;
using Moq;
using HotDice.Ui.Features;
using HotDice.Ui.Pages.Game.Components;
using HotDice.Ui.Services;
using static HotDice.Contracts.HttpResponses;

namespace HotDice.SpaTests.Handlers.KeepDice;

// H2 — covers HotDice.Ui/Features/Actions/KeepDice.cs.
public class HandleShould : HandlerTestContext
{
  [Fact]
  public async Task SendDerivedSetAsideDice_RemoveThemFromInPlay_AndUpdateTurnScore()
  {
    // Arrange: a die set aside (Five) and one still in play (Three).
    State.DiceInPlay.Add(DiceInfo.Selected(0, DieValue.Five));
    State.DiceInPlay.Add(DiceInfo.Unselected(1, DieValue.Three));

    List<int>? sentDice = null;
    Mock.Get(GameService)
      .Setup(s => s.KeepDiceAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<IEnumerable<int>>()))
      .Callback<int, int, IEnumerable<int>>((_, _, d) => sentDice = d.ToList())
      .ReturnsAsync(new KeepDiceResponse(Id: 0, TurnScore: 50));

    // Act
    await Sender.Send(new GameState.KeepDice.Action());

    // Assert
    sentDice.Should().NotBeNull("KeepDiceAsync should have been called");
    sentDice!.Should().Equal(5);
    State.DiceInPlay.Should().ContainSingle(d => d.Value == DieValue.Three)
      .Which.IsSelected.Should().BeFalse();
    State.TurnScore.Value.Should().Be(50);
  }

  [Fact]
  public async Task SendEmpty_WhenNothingSetAside()
  {
    State.DiceInPlay.Add(DiceInfo.Unselected(0, DieValue.Two));

    List<int>? sentDice = null;
    Mock.Get(GameService)
      .Setup(s => s.KeepDiceAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<IEnumerable<int>>()))
      .Callback<int, int, IEnumerable<int>>((_, _, d) => sentDice = d.ToList())
      .ReturnsAsync(new KeepDiceResponse(0, 0));

    await Sender.Send(new GameState.KeepDice.Action());

    sentDice.Should().BeEmpty();
    State.DiceInPlay.Should().ContainSingle();
  }
}
