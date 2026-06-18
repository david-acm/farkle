using FluentAssertions;
using WebApp.Client.Features;
using WebApp.Client.Pages.Game.Components;

namespace Farkle.SpaTests.Handlers.ConsumeRollAnimation;

// Covers WebApp.Client/Features/Actions/ConsumeRollAnimation.cs — the one-shot that
// clears the roll-spin flag so a recreated die (e.g. after a zone move) renders
// statically instead of replaying the spin (#139).
public class HandleShould : HandlerTestContext
{
  [Fact]
  public async Task ClearAnimateOnEveryDie()
  {
    State.DiceInPlay.Add(TrayDie.Unselected(0, DieValue.Five));
    State.DiceInPlay.Add(TrayDie.Selected(1, DieValue.Two));

    await Sender.Send(new GameState.ConsumeRollAnimation.Action());

    State.DiceInPlay.Should().OnlyContain(d => d.IsAnimated == false);
  }
}
