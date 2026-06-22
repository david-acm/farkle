using Farkle.SharedKernel.Scoring;
using FluentAssertions;

namespace Farkle.Tests.Scoring;

// The greedy machine player keeps the highest-scoring KEEP of a roll — including a multi-trick
// combination (#270) — by evaluating every subset with the shared ScoreCalculator. It drives
// the E2E players, so they pick the best combination too.
public class MachinePlayerShould
{
    [Theory]
    [InlineData(new[] { 1, 1, 1, 2, 3, 4 }, new[] { 1, 1, 1 })]            // three 1s (1000) > a lone 1
    [InlineData(new[] { 1, 5, 2, 3, 4, 4 }, new[] { 1, 5 })]              // ones & fives
    [InlineData(new[] { 2, 2, 4, 4, 6, 6 }, new[] { 2, 2, 4, 4, 6, 6 })]  // three pairs (1500)
    [InlineData(new[] { 1, 1, 1, 5, 5, 5 }, new[] { 1, 1, 1, 5, 5, 5 })]  // two triplets (2500) > either triplet
    [InlineData(new[] { 1, 2, 3, 4, 5, 6 }, new[] { 1, 2, 3, 4, 5, 6 })]  // run (1500)
    // #270 — keep the best COMBINATION (a triplet plus scoring 1s/5s), not just one trick, and
    // leave the dead (non-scoring) dice on the table.
    [InlineData(new[] { 2, 2, 2, 5, 5, 4 }, new[] { 2, 2, 2, 5, 5 })]     // 200 + 100 = 300 (drop the 4)
    [InlineData(new[] { 2, 2, 2, 5, 4, 6 }, new[] { 2, 2, 2, 5 })]        // 200 + 50 = 250
    [InlineData(new[] { 3, 3, 3, 1, 2, 6 }, new[] { 3, 3, 3, 1 })]        // 300 + 100 = 400
    [InlineData(new[] { 1, 1, 1, 5, 5, 2 }, new[] { 1, 1, 1, 5, 5 })]     // 1000 + 100 = 1100
    public void KeepTheHighestScoringCombination(int[] roll, int[] expected) =>
        MachinePlayer.ChooseBestKeep(roll).OrderBy(d => d).Should().Equal(expected.OrderBy(d => d));

    [Fact]
    public void KeepNothingOnAFarkle() =>
        MachinePlayer.ChooseBestKeep(new[] { 2, 2, 3, 3, 4, 6 }).Should().BeEmpty();
}
