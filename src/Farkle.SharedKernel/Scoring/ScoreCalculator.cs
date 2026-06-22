namespace Farkle.SharedKernel.Scoring;

// The scoring trick a set of kept dice satisfies. Names reflect the rules; the legacy domain
// validators called the four-of-a-kind "straight" and the 1-6 run a "stair".
public enum ScoringTrick
{
    None,
    SixOfAKind,    // six dice of the same value             → 3000 (#35)
    TwoTriplets,   // six dice = two three-of-a-kinds (e.g. 2,2,2,5,5,5) → 2500
    ThreePairs,    // six dice that form three pairs (all value-counts even) → 1500
    FiveOfAKind,   // five dice of the same value            → 2000 (#35)
    FourOfAKind,   // four dice of the same value          → 1000
    ThreeOfAKind,  // three of a kind → face×100, but three 1s → 1000 (#177)
    OnesAndFives,  // any mix of 1s and 5s → 100 per 1 + 50 per 5
    Run            // a full 1-6 run (all six unique)       → 1500
}

// The points and component tricks a kept selection is worth. `Tricks` lists every scoring
// component, highest-first (a single-trick keep has one; a multi-trick keep — e.g. a
// three-of-a-kind plus a pair of 5s — lists each). `Trick` is the primary (highest) component
// for back-compat, or None when nothing scores.
public readonly record struct ScoreBreakdown(
    ScoringTrick Trick,
    int Points,
    bool CanKeep,
    IReadOnlyList<ScoringTrick> Tricks);

/// <summary>
/// Pure, infra-free scoring of a set of kept dice (face values 1-6). Single source of truth
/// shared by the domain aggregate (which adds the turn-level combo doubling) and the UI
/// (which previews a selection's value before the player commits it). The trick priority
/// mirrors the aggregate's original GetNewTurnScore exactly: the first trick the dice satisfy
/// wins.
/// </summary>
public static class ScoreCalculator
{
    public static ScoreBreakdown Evaluate(IReadOnlyList<int> dice)
    {
        var (trick, points) = Score(dice);
        IReadOnlyList<ScoringTrick> tricks = trick == ScoringTrick.None ? [] : [trick];
        return new ScoreBreakdown(trick, points, CanKeep(dice), tricks);
    }

    // A keep is allowed when the dice contain a 1 or a 5, three+ of a kind, or form a full
    // six-dice three-pairs / two-triplets hand (which need not contain a 1/5 or a triplet).
    public static bool CanKeep(IReadOnlyList<int> dice) =>
        dice.Count > 0 &&
        (dice.Any(d => d is 1 or 5) ||
         dice.GroupBy(d => d).Any(g => g.Count() >= 3) ||
         IsThreePairs(dice) ||
         IsTwoTriplets(dice));

    private static (ScoringTrick Trick, int Points) Score(IReadOnlyList<int> dice)
    {
        // Six of a kind is the top trick (#35). It must be checked before three-pairs, which
        // would otherwise claim it (a six-of-a-kind has an even count) for only 1500.
        if (IsAllSame(dice, 6)) return (ScoringTrick.SixOfAKind, 3000);

        // Six-dice combos take priority — a qualifying hand always takes the higher combo.
        if (IsTwoTriplets(dice)) return (ScoringTrick.TwoTriplets, 2500);
        if (IsThreePairs(dice))  return (ScoringTrick.ThreePairs, 1500);

        // Five of a kind (#35) before four/three and before ones-and-fives (which would
        // otherwise score five 1s/5s as a mere 500/250).
        if (IsAllSame(dice, 5)) return (ScoringTrick.FiveOfAKind, 2000);

        if (IsAllSame(dice, 4)) return (ScoringTrick.FourOfAKind, 1000);

        if (IsAllSame(dice, 3))
        {
            var face = dice[0];
            return (ScoringTrick.ThreeOfAKind, face == 1 ? 1000 : face * 100);
        }

        if (dice.Count > 0 && dice.All(d => d is 1 or 5))
            return (ScoringTrick.OnesAndFives,
                    dice.Count(d => d == 1) * 100 + dice.Count(d => d == 5) * 50);

        if (dice.Count == 6 && Enumerable.Range(1, 6).All(dice.Contains))
            return (ScoringTrick.Run, 1500);

        return (ScoringTrick.None, 0);
    }

    private static bool IsAllSame(IReadOnlyList<int> dice, int count) =>
        dice.Count == count && dice.Distinct().Count() == 1;

    // Two three-of-a-kinds: six dice, exactly two distinct values, each appearing three times
    // (e.g. 2,2,2,5,5,5). Note {3,3} counts are odd, so this is never also "three pairs".
    private static bool IsTwoTriplets(IReadOnlyList<int> dice)
    {
        if (dice.Count != 6) return false;
        var counts = dice.GroupBy(d => d).Select(g => g.Count()).ToList();
        return counts.Count == 2 && counts.All(c => c == 3);
    }

    // Three pairs: six dice whose every value-count is even, so they partition into three
    // pairs — covers 2,2,4,4,6,6 (three distinct pairs), a four-of-a-kind + a pair, and a
    // six-of-a-kind.
    private static bool IsThreePairs(IReadOnlyList<int> dice) =>
        dice.Count == 6 && dice.GroupBy(d => d).All(g => g.Count() % 2 == 0);
}
