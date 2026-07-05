using Farkle.SharedKernel.Scoring;

namespace Farkle.SharedKernel.Turns;

/// <summary>
/// The stage a turn is in, expressed in a vocabulary shared by the domain and the client.
/// Each side maps its own state onto this: the domain's <c>GameStage.Rolling</c> /
/// <c>GameStage.Keeping</c> / <c>GameStage.Finished</c>, and the client's snapshot stage
/// string. Only the *stage → allowed action* rule lives here; "is it my turn" stays each
/// side's own concern (see <see cref="TurnActionPolicy"/>).
/// </summary>
public enum TurnStage
{
    /// <summary>Waiting for the in-turn player to roll (domain <c>Rolling</c>).</summary>
    AwaitingRoll,

    /// <summary>Dice are on the table waiting to be kept (domain <c>Keeping</c>).</summary>
    AwaitingKeep,

    /// <summary>The game is over — no action is valid.</summary>
    Finished
}

/// <summary>Which of the three turn actions are valid right now.</summary>
public readonly record struct ActionAvailability(bool CanRoll, bool CanKeep, bool CanPass);

/// <summary>
/// Pure, infra-free single source of truth for "which turn actions are allowed at this stage".
/// Shared by the domain (<c>GameValidator</c>'s <c>SingleRoll</c> / <c>PlayerCanPass</c> delegate
/// to it, still emitting their specific error events) and the client (<c>GameState</c> maps its
/// state into it to gate the Roll / Keep / Pass buttons). Mirrors the existing
/// <see cref="ScoreCalculator"/> precedent, which is shared the same way.
///
/// This owns only the stage rule; layering "it's my turn" on top is each side's own concern, so
/// off-turn everything is disabled regardless of what this returns.
/// </summary>
public static class TurnActionPolicy
{
    /// <summary>
    /// Evaluates which of the three turn actions (Roll / Keep / Pass) are valid right now, from
    /// the turn stage plus whether the player has acted this turn and whether the staged selection
    /// scores. Layer "it's my turn" on top of the result — off-turn, every action is disabled.
    /// </summary>
    /// <param name="stage">The turn stage.</param>
    /// <param name="hasActedThisTurn">
    /// Whether the player has already rolled or kept this turn (the domain's "last event is a
    /// Roll or Keep"). Gates Pass: you may only pass once you've committed to a roll this turn.
    /// </param>
    /// <param name="selectionScores">
    /// Whether the currently staged dice selection scores (the shared
    /// <see cref="ScoreCalculator.CanKeep"/>). Gates Keep.
    /// </param>
    public static ActionAvailability Evaluate(TurnStage stage, bool hasActedThisTurn, bool selectionScores) =>
        stage switch
        {
            // Turn start (or after a keep) — you can roll, and you can pass only once you've acted.
            TurnStage.AwaitingRoll => new ActionAvailability(
                CanRoll: true,
                CanKeep: false,
                CanPass: hasActedThisTurn),

            // After a roll — no second roll before a keep; keep only if the selection scores; pass allowed.
            TurnStage.AwaitingKeep => new ActionAvailability(
                CanRoll: false,
                CanKeep: selectionScores,
                CanPass: hasActedThisTurn),

            // Finished — nothing is valid.
            _ => new ActionAvailability(false, false, false)
        };
}
