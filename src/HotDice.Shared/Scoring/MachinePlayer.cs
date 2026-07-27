namespace HotDice.SharedKernel.Scoring;

/// <summary>
/// A greedy machine player: given a roll, it always keeps the single highest-scoring trick,
/// scored by the shared <see cref="ScoreCalculator"/>. Pure and infra-free, so it can drive
/// automated play, demos and tests. It evaluates every keepable subset of the rolled dice and
/// returns the one worth the most points (empty on a Farkle — nothing scores).
/// </summary>
public static class MachinePlayer
{
    // The face values of the best-scoring trick to keep (empty on a Farkle). Use this when
    // you act on values (e.g. the SetDiceAside command, which takes a die value).
    public static IReadOnlyList<int> ChooseBestKeep(IReadOnlyList<int> rolledDice)
    {
        var indices = ChooseBestKeepIndices(rolledDice);
        var values = new int[indices.Count];
        for (var i = 0; i < indices.Count; i++)
            values[i] = rolledDice[indices[i]];
        return values;
    }

    // The positions (within the roll) of the best-scoring trick to keep (empty on a Farkle).
    // Use this when you act by position — e.g. tapping the n-th die in the UI tray, which
    // renders dice in roll order.
    public static IReadOnlyList<int> ChooseBestKeepIndices(IReadOnlyList<int> rolledDice)
    {
        int[] best = [];
        var bestPoints = 0;

        // A roll is at most six dice, so enumerating the 2^n subsets is trivial. Index-based
        // masks handle duplicate faces correctly (each die is its own bit).
        var n = rolledDice.Count;
        for (var mask = 1; mask < (1 << n); mask++)
        {
            var subset = new List<int>(n);
            var indices = new List<int>(n);
            for (var i = 0; i < n; i++)
                if ((mask & (1 << i)) != 0)
                {
                    subset.Add(rolledDice[i]);
                    indices.Add(i);
                }

            var points = ScoreCalculator.Evaluate(subset).Points;
            if (points > bestPoints)
            {
                bestPoints = points;
                best = indices.ToArray();
            }
        }

        return best;
    }
}
