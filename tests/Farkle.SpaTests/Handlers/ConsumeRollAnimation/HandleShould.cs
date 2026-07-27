using FluentAssertions;
using Farkle.Ui.Features;
using Farkle.Ui.Pages.Game.Components;

namespace Farkle.SpaTests.Handlers.ConsumeRollAnimation;

// Covers Farkle.Ui/Features/Actions/ConsumeRollAnimation.cs — the one-shot that
// clears the roll-spin flag so a recreated die (e.g. after a zone move) renders
// statically instead of replaying the spin (#139).
public class HandleShould : HandlerTestContext
{
  [Fact]
  public async Task ClearAnimateOnEveryDie()
  {
    State.DiceInPlay.Add(DiceInfo.Unselected(0, DieValue.Five));
    State.DiceInPlay.Add(DiceInfo.Selected(1, DieValue.Two));

    await Sender.Send(new GameState.ConsumeRollAnimation.Action());

    State.DiceInPlay.Should().OnlyContain(d => d.IsAnimated == false);
  }
}
