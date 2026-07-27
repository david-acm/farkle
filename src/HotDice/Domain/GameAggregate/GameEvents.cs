using System.Collections.Immutable;
using HotDice.SharedKernel.Turns;

namespace HotDice.Domain.GameAggregate;

// Events are public (ADR 0004): Marten discovers the GameState.Apply(<Event>) overloads and the
// slice handlers return them. Marten registers event types by CLR type (greenfield data, ADR 0002),
// so the Eventuous [EventType] attributes are gone. Never modify a V1 schema — add a V2.
//
// Events stay in the shared kernel rather than moving into the slice that emits them (unlike the
// commands, #329): every event here is folded by GameState — the innermost layer — so a slice-local
// event would invert the dependency rule. Some have no single owner either (PlayedOutOfTurn is
// raised by GameValidator and reached by five slices), and unlike commands they are the persisted
// contract Marten replays.
public static class GameEvents
{
  public static class V1
  {
    public record GameStarted(int Id);

    public record PlayerJoined(int Id, string Name);

    public record GamePlayStarted(int StartedByPlayerId);

    public record DiceRolled(int PlayerId, int[] Dice, Score TurnScore);

    public record DiceKept(int PlayerId, int[] Dice, int[] TableCenter, int NewTurnScore);

    // #159 — set aside / put back: a transient, non-scoring selection of which rolled dice the
    // player intends to keep. First-class so they can be persisted and broadcast to spectators.
    public record DiceSetAside(int PlayerId, int Die);

    public record DiceReturned(int PlayerId, int Die);

    public record DieNotAvailableToSetAside(int PlayerId, int Die) : IErrorEvent;

    public record DieNotSetAside(int PlayerId, int Die) : IErrorEvent;

    public record TurnPassed(int PlayerId, ImmutableArray<Player> PlayerOrder, int GameScore);

    public record PlayedOutOfTurn(int TriedToPlay, int ExpectedPlayer) : IErrorEvent;

    public record RolledTwice(int Player) : IErrorEvent;

    public record PassedWithoutRolling(int PlayerId) : IErrorEvent;

    public record OnlyHostCanStartGame(int PlayerId, int HostId) : IErrorEvent;

    public record NotEnoughPlayers(int PlayerCount, int Minimum) : IErrorEvent;

    public record GameAlreadyInPlay(GameStage Stage) : IErrorEvent;

    public record GameWon(int PlayerId, int Score);
  }

  public static class V2
  {
    // V2 of PlayerJoined adds the player's identity Color (assigned from PlayerColors by join
    // order). V1.PlayerJoined is left untouched; GameState handles both, deriving the colour from
    // the id for V1 so older streams still render a consistent colour.
    public record PlayerJoined(int Id, string Name, string Color);

    public record DiceRolled(int PlayerId, int[] Dice, Score TurnScore, GameStage Stage);

    public record DiceKept(int PlayerId, int[] Dice, int[] TableCenter, int NewTurnScore, GameStage Stage);
  }
}
