namespace HotDice.SharedKernel.Scoring;

// A single scoring component of a kept selection. Names reflect the rules; the legacy domain
// validators called the four-of-a-kind a "straight" and the 1-6 run a "stair". Lone 1s/5s are
// their own components (Ones / Fives) so a selection can read e.g. "ones + 3 of a kind" (#274).
public enum ScoringTrick
{
    None,
    SixOfAKind,    // six dice of the same value             → 3000 (#35)
    TwoTriplets,   // six dice = two three-of-a-kinds (e.g. 2,2,2,5,5,5) → 2500
    ThreePairs,    // six dice that form three pairs (all value-counts even) → 1500
    FiveOfAKind,   // five dice of the same value            → 2000 (#35)
    FourOfAKind,   // four dice of the same value            → 1000
    ThreeOfAKind,  // three of a kind → face×100, but three 1s → 1000 (#177)
    Ones,          // leftover single 1s → 100 each
    Fives,         // leftover single 5s → 50 each
    Run            // a full 1-6 run (all six unique)        → 1500
}

// The points a kept selection is worth and the component tricks it is made of, listed in
// ascending die-value order (e.g. {1,3,3,3} → [Ones, ThreeOfAKind]).
public readonly record struct ScoreBreakdown(
    int Points,
    bool CanKeep,
    IReadOnlyList<ScoringTrick> Tricks);

/// <summary>
/// Pure, infra-free scoring of a set of kept dice (face values 1-6). Single source of truth
/// shared by the domain aggregate (which adds the turn-level combo doubling) and the UI
/// (which previews a selection's value before the player commits it).
///
/// A selection may contain MORE THAN ONE trick (e.g. a three-of-a-kind plus a pair of 5s); the
/// score is the SUM of its components (#270). The result is the maximum of two readings: a
/// single whole-set six-dice combo (two triplets / three pairs / run), or a per-face
/// decomposition (each face's largest n-of-a-kind, plus leftover single 1s/5s). Non-scoring
/// "dead" dice simply contribute nothing; a set is keepable when at least one die scores.
/// </summary>
public static class ScoreCalculator
{
    public static ScoreBreakdown Evaluate(IReadOnlyList<int> dice)
    {
        var (points, tricks) = BestScoring(dice);
        return new ScoreBreakdown(points, points > 0, tricks);
    }

    // A set is keepable iff at least one die scores. This is the lenient "is there anything
    // worth keeping" predicate — shared by the keep gate AND the roll/HotDice check (a roll is a
    // bust only when NOTHING scores), so it must stay lenient. Dead dice in a selection simply
    // don't add points (see BestScoring); they don't make the whole selection unkeepable.
    public static bool CanKeep(IReadOnlyList<int> dice) => BestScoring(dice).Points > 0;

    private static (int Points, IReadOnlyList<ScoringTrick> Tricks) BestScoring(IReadOnlyList<int> dice)
    {
        var best = Decompose(dice);

        if (WholeSetCombo(dice) is { } combo && combo.Points > best.Points)
            best = (combo.Points, [combo.Trick]);

        return best;
    }

    // The multi-value six-dice combos. (A six-of-a-kind is a single face, so Decompose scores it.)
    private static (int Points, ScoringTrick Trick)? WholeSetCombo(IReadOnlyList<int> dice)
    {
        if (IsTwoTriplets(dice)) return (2500, ScoringTrick.TwoTriplets);
        if (IsThreePairs(dice))  return (1500, ScoringTrick.ThreePairs);
        if (dice.Count == 6 && Enumerable.Range(1, 6).All(dice.Contains)) return (1500, ScoringTrick.Run);
        return null;
    }

    // Sums each face's largest n-of-a-kind (atomic — four 1s stays a four-of-a-kind, not 3+1)
    // plus any leftover single 1s/5s (as Ones / Fives components). Non-scoring "dead" dice (a
    // 2/3/4/6 with fewer than three of it) contribute nothing. Faces are walked in ascending
    // value order, so the component list reads low-to-high (e.g. [Ones, ThreeOfAKind]).
    private static (int Points, IReadOnlyList<ScoringTrick> Tricks) Decompose(IReadOnlyList<int> dice)
    {
        var components = new List<ScoringTrick>();
        var points = 0;

        for (var value = 1; value <= 6; value++)
        {
            var count = dice.Count(d => d == value);
            if (count == 0) continue;

            if (count >= 3)
            {
                var (p, trick) = NOfAKind(value, count);
                points += p;
                components.Add(trick);
            }
            else if (value == 1)
            {
                points += count * 100;
                components.Add(ScoringTrick.Ones);
            }
            else if (value == 5)
            {
                points += count * 50;
                components.Add(ScoringTrick.Fives);
            }
            // else: a 2/3/4/6 with fewer than three of it scores nothing — skip it.
        }

        return (points, components);
    }

    // The score and trick for n dice of the same value (n >= 3): three of a kind is face×100
    // (three 1s special-cased to 1000, #177); four/five/six of a kind are flat 1000/2000/3000.
    private static (int Points, ScoringTrick Trick) NOfAKind(int value, int count) => count switch
    {
        >= 6 => (3000, ScoringTrick.SixOfAKind),
        5    => (2000, ScoringTrick.FiveOfAKind),
        4    => (1000, ScoringTrick.FourOfAKind),
        _    => (value == 1 ? 1000 : value * 100, ScoringTrick.ThreeOfAKind) // count == 3
    };

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
