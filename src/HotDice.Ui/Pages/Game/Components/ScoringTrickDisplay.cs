using HotDice.SharedKernel.Scoring;

namespace HotDice.Ui.Pages.Game.Components;

// Human-friendly names for the scoring tricks, shared by the turn-score preview and the
// scoring reference page so the labels never drift. Casual, content-specific wording (#274):
// "ones", "fives", "3 of a kind", joined with " + " for a multi-trick selection.
public static class ScoringTrickDisplay
{
  // A selection can hold several tricks (e.g. a three-of-a-kind plus a pair of 5s, #270); show
  // them all, in the breakdown's order, joined with " + " (e.g. "ones + 3 of a kind").
  public static string ToDisplayName(this ScoreBreakdown breakdown) =>
    breakdown.Tricks.Count > 0
      ? string.Join(" + ", breakdown.Tricks.Select(t => t.ToDisplayName()))
      : ScoringTrick.None.ToDisplayName();

  public static string ToDisplayName(this ScoringTrick trick) => trick switch
  {
    ScoringTrick.SixOfAKind   => "6 of a kind",
    ScoringTrick.FiveOfAKind  => "5 of a kind",
    ScoringTrick.FourOfAKind  => "4 of a kind",
    ScoringTrick.ThreeOfAKind => "3 of a kind",
    ScoringTrick.TwoTriplets  => "two triplets",
    ScoringTrick.ThreePairs   => "three pairs",
    ScoringTrick.Run          => "run",
    ScoringTrick.Ones         => "ones",
    ScoringTrick.Fives        => "fives",
    _                         => "no score"
  };
}
