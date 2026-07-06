namespace Farkle.SharedKernel.Turns;

/// <summary>
/// The lifecycle stage of a game, shared by the domain aggregate and the client so both speak
/// one vocabulary (the domain state machine and the client's button gating via
/// <see cref="TurnActionPolicy"/>).
///
/// IMPORTANT — this enum is persisted by ordinal: V2 events (<c>DiceRolled</c> / <c>DiceKept</c>)
/// serialize their <c>Stage</c> field, and the read-model snapshot stores <c>(int)GameStage</c>.
/// Never reorder or renumber the members and never change their names — append new values at the
/// end only, or already-stored events and snapshots will decode to the wrong stage.
/// </summary>
public enum GameStage
{
  None,
  Rolling,
  Keeping,
  Finished,
  // Appended at the end to preserve existing ordinals: V2 events serialize GameStage,
  // so inserting a value mid-enum would corrupt already-stored events.
  WaitingForPlayers
}
