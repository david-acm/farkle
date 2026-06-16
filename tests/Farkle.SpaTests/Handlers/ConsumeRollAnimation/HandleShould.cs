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
    State.DiceInPlay.Add(new TrayDie(0, DieValue.Five, "Rolled")  { Animate = true });
    State.DiceInPlay.Add(new TrayDie(1, DieValue.Two,  "SetAside") { Animate = true });

    await Sender.Send(new GameState.ConsumeRollAnimation.Action());

    State.DiceInPlay.Should().OnlyContain(d => d.Animate == false);
  }
}
