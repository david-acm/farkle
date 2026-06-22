using Farkle.SharedKernel.Scoring;
using FluentAssertions;

namespace Farkle.Tests.Scoring;

// Direct unit tests for the shared, infra-free scoring calculator (the single source of
// truth now reused by the domain aggregate and the UI preview).
public class ScoreCalculatorShould
{
  [Theory]
  [InlineData(new[] { 1 },             ScoringTrick.OnesAndFives, 100)]
  [InlineData(new[] { 5 },             ScoringTrick.OnesAndFives, 50)]
  [InlineData(new[] { 1, 5, 1 },       ScoringTrick.OnesAndFives, 250)]
  [InlineData(new[] { 3, 3, 3 },       ScoringTrick.ThreeOfAKind, 300)]
  [InlineData(new[] { 1, 1, 1 },       ScoringTrick.ThreeOfAKind, 1000)] // three 1s special (#177)
  [InlineData(new[] { 4, 4, 4, 4 },    ScoringTrick.FourOfAKind, 1000)]
  [InlineData(new[] { 1, 2, 3, 4, 5, 6 }, ScoringTrick.Run, 1500)]
  [InlineData(new[] { 2, 2, 4, 4, 6, 6 }, ScoringTrick.ThreePairs, 1500)]   // three distinct pairs
  [InlineData(new[] { 3, 3, 3, 3, 5, 5 }, ScoringTrick.ThreePairs, 1500)]   // quad + pair = three pairs
  [InlineData(new[] { 2, 2, 2, 5, 5, 5 }, ScoringTrick.TwoTriplets, 2500)]  // two three-of-a-kinds
  [InlineData(new[] { 1, 1, 5, 5, 2, 2 }, ScoringTrick.ThreePairs, 1500)]   // beats ones+fives (highest 6-dice)
  // #270 — multiple tricks in one keep score the SUM (primary = highest-value component).
  [InlineData(new[] { 2, 2, 2, 5, 5 }, ScoringTrick.ThreeOfAKind, 400)]  // three 2s (200) + two 5s (100)
  [InlineData(new[] { 2, 2, 2, 5 },    ScoringTrick.ThreeOfAKind, 250)]  // three 2s (200) + one 5 (50)
  [InlineData(new[] { 3, 3, 3, 1 },    ScoringTrick.ThreeOfAKind, 400)]  // three 3s (300) + one 1 (100)
  [InlineData(new[] { 1, 1, 1, 5, 5 }, ScoringTrick.ThreeOfAKind, 1100)] // three 1s (1000) + two 5s (100)
  // #35 — six of a kind = 3000 for every value (must beat the three-pairs reading of an
  // even count); five of a kind = 2000 (must beat ones/fives for 1s and 5s).
  [InlineData(new[] { 1, 1, 1, 1, 1, 1 }, ScoringTrick.SixOfAKind, 3000)]
  [InlineData(new[] { 2, 2, 2, 2, 2, 2 }, ScoringTrick.SixOfAKind, 3000)]
  [InlineData(new[] { 3, 3, 3, 3, 3, 3 }, ScoringTrick.SixOfAKind, 3000)]
  [InlineData(new[] { 4, 4, 4, 4, 4, 4 }, ScoringTrick.SixOfAKind, 3000)]
  [InlineData(new[] { 5, 5, 5, 5, 5, 5 }, ScoringTrick.SixOfAKind, 3000)]
  [InlineData(new[] { 6, 6, 6, 6, 6, 6 }, ScoringTrick.SixOfAKind, 3000)]
  [InlineData(new[] { 1, 1, 1, 1, 1 },    ScoringTrick.FiveOfAKind, 2000)] // beats five ones (500)
  [InlineData(new[] { 5, 5, 5, 5, 5 },    ScoringTrick.FiveOfAKind, 2000)] // beats five fives (250)
  [InlineData(new[] { 3, 3, 3, 3, 3 },    ScoringTrick.FiveOfAKind, 2000)]
  [InlineData(new[] { 2, 3 },          ScoringTrick.None, 0)]
  public void ScoreEachTrick(int[] dice, ScoringTrick expectedTrick, int expectedPoints)
  {
    var breakdown = ScoreCalculator.Evaluate(dice);

    breakdown.Trick.Should().Be(expectedTrick);
    breakdown.Points.Should().Be(expectedPoints);
  }

  [Fact]
  public void PreferFourOfAKindOverOnesAndFives()
  {
    // Four 1s score as a four-of-a-kind (1000), not as four ones (400) — n-of-a-kind is atomic.
    ScoreCalculator.Evaluate(new[] { 1, 1, 1, 1 })
      .Should().BeEquivalentTo(
        new ScoreBreakdown(ScoringTrick.FourOfAKind, 1000, true, new[] { ScoringTrick.FourOfAKind }));
  }

  [Fact]
  public void ListEveryComponentTrick_ForAMultiTrickKeep()
  {
    // #270 — a three-of-a-kind plus a pair of 5s lists BOTH tricks (highest-first) and sums.
    var breakdown = ScoreCalculator.Evaluate(new[] { 2, 2, 2, 5, 5 });

    breakdown.Points.Should().Be(400);
    breakdown.CanKeep.Should().BeTrue();
    breakdown.Tricks.Should().Equal(ScoringTrick.ThreeOfAKind, ScoringTrick.OnesAndFives);
  }

  [Fact]
  public void PreferTheWholeSetCombo_WhenHigherThanTheDecomposition()
  {
    // 2,2,2,5,5,5 decomposes to 200+500=700, but the whole-set two-triplets is 2500 → take the max.
    ScoreCalculator.Evaluate(new[] { 2, 2, 2, 5, 5, 5 })
      .Should().BeEquivalentTo(
        new ScoreBreakdown(ScoringTrick.TwoTriplets, 2500, true, new[] { ScoringTrick.TwoTriplets }));
  }

  [Theory]
  [InlineData(new[] { 1 },       true)]
  [InlineData(new[] { 5 },       true)]
  [InlineData(new[] { 2, 2, 2 }, true)]  // three of a kind is keepable even without 1s/5s
  [InlineData(new[] { 2, 2, 4, 4, 6, 6 }, true)]  // three pairs — keepable despite no 1/5 or triplet
  [InlineData(new[] { 2, 3 },    false)]
  [InlineData(new[] { 2, 2 },    false)] // only two of a kind, no 1/5
  [InlineData(new[] { 2, 2, 2, 5, 5 }, true)]  // #270 — triplet + scoring pair is keepable
  [InlineData(new[] { 2, 2, 2, 4 },    false)] // #270 — triplet + a dead die is NOT keepable
  public void DecideWhatIsKeepable(int[] dice, bool canKeep) =>
    ScoreCalculator.CanKeep(dice).Should().Be(canKeep);
}
