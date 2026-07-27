using HotDice.SharedKernel.Scoring;
using FluentAssertions;

namespace HotDice.Tests.Scoring;

// Direct unit tests for the shared, infra-free scoring calculator (the single source of
// truth reused by the domain aggregate and the UI preview).
public class ScoreCalculatorShould
{
  [Theory]
  [InlineData(new[] { 1 },                100)]
  [InlineData(new[] { 5 },                50)]
  [InlineData(new[] { 1, 5, 1 },          250)]
  [InlineData(new[] { 3, 3, 3 },          300)]
  [InlineData(new[] { 1, 1, 1 },          1000)] // three 1s special (#177)
  [InlineData(new[] { 4, 4, 4, 4 },       1000)]
  [InlineData(new[] { 1, 1, 1, 1, 1 },    2000)] // five of a kind (#35)
  [InlineData(new[] { 1, 1, 1, 1, 1, 1 }, 3000)] // six of a kind (#35)
  [InlineData(new[] { 1, 2, 3, 4, 5, 6 }, 1500)] // run
  [InlineData(new[] { 2, 2, 4, 4, 6, 6 }, 1500)] // three pairs
  [InlineData(new[] { 2, 2, 2, 5, 5, 5 }, 2500)] // two triplets
  // #270 — multiple tricks in one keep score the SUM.
  [InlineData(new[] { 2, 2, 2, 5, 5 },    300)]  // three 2s (200) + two 5s (100)
  [InlineData(new[] { 2, 2, 2, 5 },       250)]  // three 2s (200) + one 5 (50)
  [InlineData(new[] { 3, 3, 3, 1 },       400)]  // three 3s (300) + one 1 (100)
  [InlineData(new[] { 1, 1, 1, 5, 5 },    1100)] // three 1s (1000) + two 5s (100)
  [InlineData(new[] { 2, 3 },             0)]
  public void ScoreThePoints(int[] dice, int expectedPoints) =>
    ScoreCalculator.Evaluate(dice).Points.Should().Be(expectedPoints);

  // #274 — components are listed in ascending die-value order, with lone 1s/5s split into their
  // own Ones / Fives components (so the label reads e.g. "ones + 3 of a kind").
  [Theory]
  [InlineData(new[] { 5 },                new[] { ScoringTrick.Fives })]
  [InlineData(new[] { 1 },                new[] { ScoringTrick.Ones })]
  [InlineData(new[] { 1, 5, 1 },          new[] { ScoringTrick.Ones, ScoringTrick.Fives })]
  [InlineData(new[] { 3, 3, 3 },          new[] { ScoringTrick.ThreeOfAKind })]
  [InlineData(new[] { 4, 4, 4, 4 },       new[] { ScoringTrick.FourOfAKind })]
  [InlineData(new[] { 2, 2, 2, 5, 5 },    new[] { ScoringTrick.ThreeOfAKind, ScoringTrick.Fives })]
  [InlineData(new[] { 3, 3, 3, 1 },       new[] { ScoringTrick.Ones, ScoringTrick.ThreeOfAKind })]
  [InlineData(new[] { 1, 1, 1, 5, 5 },    new[] { ScoringTrick.ThreeOfAKind, ScoringTrick.Fives })]
  [InlineData(new[] { 2, 2, 2, 5, 5, 5 }, new[] { ScoringTrick.TwoTriplets })]
  [InlineData(new[] { 2, 2, 4, 4, 6, 6 }, new[] { ScoringTrick.ThreePairs })]
  [InlineData(new[] { 1, 2, 3, 4, 5, 6 }, new[] { ScoringTrick.Run })]
  [InlineData(new[] { 2, 3 },             new ScoringTrick[] { })]
  public void ListComponentTricksLowToHigh(int[] dice, ScoringTrick[] expected) =>
    ScoreCalculator.Evaluate(dice).Tricks.Should().Equal(expected);

  [Theory]
  [InlineData(new[] { 1 },                true)]
  [InlineData(new[] { 5 },                true)]
  [InlineData(new[] { 2, 2, 2 },          true)]  // three of a kind is keepable even without 1s/5s
  [InlineData(new[] { 2, 2, 4, 4, 6, 6 }, true)]  // three pairs — keepable despite no 1/5 or triplet
  [InlineData(new[] { 2, 2, 2, 5, 5 },    true)]  // #270 — triplet + scoring pair is keepable
  [InlineData(new[] { 2, 2, 2, 4 },       true)]  // keepable via the triplet; the lone 4 scores nothing
  [InlineData(new[] { 2, 3 },             false)]
  [InlineData(new[] { 2, 2 },             false)] // only two of a kind, no 1/5
  public void DecideWhatIsKeepable(int[] dice, bool canKeep) =>
    ScoreCalculator.CanKeep(dice).Should().Be(canKeep);
}
