using HotDice.SharedKernel.Turns;
using FluentAssertions;

namespace HotDice.Tests.Turns;

// Direct unit tests for the shared, infra-free turn-action policy — the single source of
// truth for "which of Roll / Keep / Pass is valid at this stage", reused by the domain
// validators (SingleRoll / PlayerCanPass) and the client's button gating.
public class TurnActionPolicyShould
{
  [Theory]
  // stage,             hasActed, selectionScores, CanRoll, CanKeep, CanPass
  // Turn start — can only roll; no keep (nothing rolled), no pass (haven't acted).
  [InlineData(GameStage.Rolling,  false, false, true,  false, false)]
  // After a roll — no second roll; keep only if the selection scores; pass now allowed.
  [InlineData(GameStage.Keeping,  true,  true,  false, true,  true)]
  // After a roll with a non-scoring selection (a "farkle" in progress) — keep is disabled.
  [InlineData(GameStage.Keeping,  true,  false, false, false, true)]
  // After a keep, back to rolling — can roll again OR pass (already acted this turn).
  [InlineData(GameStage.Rolling,  true,  false, true,  false, true)]
  // Finished — nothing is valid, whatever the other inputs.
  [InlineData(GameStage.Finished, true,  true,  false, false, false)]
  [InlineData(GameStage.Finished, false, false, false, false, false)]
  public void GateEachActionByStage(
    GameStage stage, bool hasActed, bool selectionScores,
    bool canRoll, bool canKeep, bool canPass)
  {
    var availability = TurnActionPolicy.Evaluate(stage, hasActed, selectionScores);

    availability.Should().Be(new ActionAvailability(canRoll, canKeep, canPass));
  }
}
