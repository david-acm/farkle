namespace HotDice.SharedKernel.Turns;

/// <summary>Which of the three turn actions are valid right now.</summary>
public readonly record struct ActionAvailability(bool CanRoll, bool CanKeep, bool CanPass);

/// <summary>
/// Pure, infra-free single source of truth for "which turn actions are allowed at this stage".
/// Shared by the domain (<c>GameValidator</c>'s <c>SingleRoll</c> / <c>PlayerCanPass</c> delegate
/// to it, still emitting their specific error events) and the client (<c>GameState</c> maps its
/// state into it to gate the Roll / Keep / Pass buttons). Mirrors the existing
/// <c>ScoreCalculator</c> precedent, which is shared the same way.
///
/// This owns only the stage rule; layering "it's my turn" on top is each side's own concern, so
/// off-turn everything is disabled regardless of what this returns.
/// </summary>
public static class TurnActionPolicy
{
    /// <summary>
    /// Evaluates which of the three turn actions (Roll / Keep / Pass) are valid right now, from
    /// the game stage plus whether the player has acted this turn and whether the staged selection
    /// scores. Layer "it's my turn" on top of the result — off-turn, every action is disabled.
    /// </summary>
    /// <param name="stage">The game stage.</param>
    /// <param name="hasActedThisTurn">
    /// Whether the player has already rolled or kept this turn (the domain's "last event is a
    /// Roll or Keep"). Gates Pass: you may only pass once you've committed to a roll this turn.
    /// </param>
    /// <param name="selectionScores">
    /// Whether the currently staged dice selection scores (the shared <c>ScoreCalculator.CanKeep</c>).
    /// Gates Keep.
    /// </param>
    public static ActionAvailability Evaluate(GameStage stage, bool hasActedThisTurn, bool selectionScores) =>
        stage switch
        {
            // Awaiting a roll (turn start, or after a keep) — you can roll, and you can pass only
            // once you've acted this turn.
            GameStage.Rolling => new ActionAvailability(
                CanRoll: true,
                CanKeep: false,
                CanPass: hasActedThisTurn),

            // Dice are on the table — no second roll before a keep; keep only if the selection
            // scores; pass allowed.
            GameStage.Keeping => new ActionAvailability(
                CanRoll: false,
                CanKeep: selectionScores,
                CanPass: hasActedThisTurn),

            // None / Finished / WaitingForPlayers — no in-turn action is valid.
            _ => new ActionAvailability(false, false, false)
        };
}
