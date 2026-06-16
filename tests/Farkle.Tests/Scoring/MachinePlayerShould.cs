using Farkle.SharedKernel.Scoring;
using FluentAssertions;

namespace Farkle.Tests.Scoring;

// The greedy machine player always keeps the single highest-scoring trick of a roll.
public class MachinePlayerShould
{
    [Theory]
    [InlineData(new[] { 1, 1, 1, 2, 3, 4 }, new[] { 1, 1, 1 })]            // three 1s (1000) > a lone 1
    [InlineData(new[] { 1, 5, 2, 3, 4, 4 }, new[] { 1, 5 })]              // ones & fives
    [InlineData(new[] { 2, 2, 4, 4, 6, 6 }, new[] { 2, 2, 4, 4, 6, 6 })]  // three pairs (1500)
    [InlineData(new[] { 1, 1, 1, 5, 5, 5 }, new[] { 1, 1, 1, 5, 5, 5 })]  // two triplets (2500) > either triplet
    [InlineData(new[] { 1, 2, 3, 4, 5, 6 }, new[] { 1, 2, 3, 4, 5, 6 })]  // run (1500)
    public void KeepTheHighestScoringTrick(int[] roll, int[] expected) =>
        MachinePlayer.ChooseBestKeep(roll).OrderBy(d => d).Should().Equal(expected.OrderBy(d => d));

    [Fact]
    public void KeepNothingOnAFarkle() =>
        MachinePlayer.ChooseBestKeep(new[] { 2, 2, 3, 3, 4, 6 }).Should().BeEmpty();
}
