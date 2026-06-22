using Farkle.SharedKernel.Scoring;

namespace WebApp.Client.Pages.Game.Components;

// Human-friendly names for the scoring tricks, shared by the turn-score preview and the
// scoring reference page so the labels never drift.
public static class ScoringTrickDisplay
{
  // #270 — a selection can hold several tricks (e.g. a three-of-a-kind plus a pair of 5s); show
  // them all, highest-first, joined with " + ". Falls back to the primary trick's name when the
  // component list is empty (e.g. a non-scoring selection).
  public static string ToDisplayName(this ScoreBreakdown breakdown) =>
    breakdown.Tricks.Count > 0
      ? string.Join(" + ", breakdown.Tricks.Select(t => t.ToDisplayName()))
      : breakdown.Trick.ToDisplayName();

  public static string ToDisplayName(this ScoringTrick trick) => trick switch
  {
    ScoringTrick.SixOfAKind   => "Six of a kind",
    ScoringTrick.TwoTriplets  => "Two triplets",
    ScoringTrick.ThreePairs   => "Three pairs",
    ScoringTrick.FiveOfAKind  => "Five of a kind",
    ScoringTrick.FourOfAKind  => "Four of a kind",
    ScoringTrick.ThreeOfAKind => "Three of a kind",
    ScoringTrick.OnesAndFives => "Ones & fives",
    ScoringTrick.Run          => "Run (1-6)",
    _                         => "No score"
  };
}
