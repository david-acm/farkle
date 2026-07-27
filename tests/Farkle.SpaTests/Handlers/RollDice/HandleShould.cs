using Ardalis.Result;
using FluentAssertions;
using Moq;
using Farkle.Ui.Features;
using Farkle.Ui.Pages.Game.Components;
using Farkle.Ui.Services;

namespace Farkle.SpaTests.Handlers.RollDice;

// H1 — covers Farkle.Ui/Features/Actions/RollDiceAction.cs.
public class HandleShould : HandlerTestContext
{
  [Fact]
  public async Task ReplaceDiceInPlay_WithRolledIdentifier_OnSuccess()
  {
    Mock.Get(GameService)
      .Setup(s => s.RollDiceAsync(It.IsAny<int>(), It.IsAny<int>()))
      .ReturnsAsync(Result<IList<DieValue>>.Success(
        new List<DieValue> { DieValue.Five, DieValue.One, DieValue.Three }));

    await Sender.Send(new GameState.RollDice.Action());

    State.DiceInPlay.Should().HaveCount(3);
    State.DiceInPlay.Select(d => d.Value)
      .Should().Equal(DieValue.Five, DieValue.One, DieValue.Three);
    State.DiceInPlay.Should().OnlyContain(d => !d.IsSelected);
    State.DiceInPlay.Select(d => d.Index).Should().Equal(0, 1, 2);
  }

  [Fact]
  public async Task SetError_OnFailure()
  {
    Mock.Get(GameService)
      .Setup(s => s.RollDiceAsync(It.IsAny<int>(), It.IsAny<int>()))
      .ReturnsAsync(Result<IList<DieValue>>.Error(new ErrorList(["Not your turn", "Already rolled"])));

    await Sender.Send(new GameState.RollDice.Action());

    State.Error.Should().BeTrue();
    State.ErrorMessage.Should().Contain("Not your turn").And.Contain("Already rolled");
    State.DiceInPlay.Should().BeEmpty("a failed roll must not populate dice");
  }
}
