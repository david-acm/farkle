using Farkle.Domain.GameAggregate;
using Farkle.Features.BeginGame;
using Farkle.Features.JoinPlayer;
using Farkle.Features.KeepDice;
using Farkle.Features.PassTurn;
using Farkle.Features.ReturnDice;
using Farkle.Features.RollDice;
using Farkle.Features.SetDiceAside;
using Farkle.Features.StartGame;
using Farkle.SharedKernel.Turns;

namespace Farkle.Tests;

// Test-only reconstruction of the old aggregate's command surface over the pure #301 deciders +
// GameState.Fold. The Eventuous Game aggregate was removed at the Marten cutover (ADR 0004); in
// production the same deciders are driven by Wolverine [AggregateHandler]s. This keeps the large
// existing domain-test suite in its familiar (game.X → assert State/Changes) shape without change.
internal sealed class Game
{
  private readonly IRandom       _random;
  private readonly List<object>  _loaded  = new();
  private readonly List<object>  _changes = new();

  public Game(IRandom? random = null) => _random = random ?? new DefaultRandomProvider();

  public GameState State { get; private set; } = new();

  // Uncommitted events since (a fresh instance or) the last Load — the Eventuous Changes semantics.
  public IReadOnlyCollection<object> Changes => _changes;

  // Full current event set (loaded + new) — the Eventuous Current semantics.
  public IReadOnlyCollection<object> Current => _loaded.Concat(_changes).ToList();

  // Rehydrate from prior events without recording them as changes (mirrors aggregate load).
  public void Load(IEnumerable<object> events)
  {
    foreach (var e in events)
    {
      _loaded.Add(e);
      State = GameState.Fold(State, e);
    }
  }

  public void Start(StartGameCommand c)        => Apply(StartGameDecider.Decide(c, State));
  public void JoinPlayer(JoinPlayerCommand c)  => Apply(JoinPlayerDecider.Decide(c, State));
  public void BeginGame(BeginGameCommand c)    => Apply(BeginGameDecider.Decide(c, State));
  public void KeepDice(KeepDiceCommand c)      => Apply(KeepDiceDecider.Decide(c, State));
  public void PassTurn(PassTurnCommand c)      => Apply(PassTurnDecider.Decide(c, State));
  public void SetDiceAside(SetDiceAsideCommand c) => Apply(SetDiceAsideDecider.Decide(c, State));
  public void ReturnDice(ReturnDiceCommand c)  => Apply(ReturnDiceDecider.Decide(c, State));

  public void RollDiceV2(RollDiceCommand c) =>
    Apply(RollDiceDecider.Decide(c, State, Dice.FromNewRoll(_random, State.DiceToRoll)));

  // V1 roll/keep paths retained for the tests that exercise the older event shapes: reuse the
  // decider's validation + scoring, then downcast the emitted V2 event to its V1 counterpart.
  public void RollDiceV1(RollDiceCommand c) =>
    Apply(RollDiceDecider.Decide(c, State, Dice.FromNewRoll(_random, State.DiceToRoll))
      .Select(e => e is GameEvents.V2.DiceRolled r
        ? new GameEvents.V1.DiceRolled(r.PlayerId, r.Dice, r.TurnScore)
        : e));

  public void KeepDiceV2(KeepDiceCommand c) =>
    Apply(KeepDiceDecider.Decide(c, State)
      .Select(e => e is GameEvents.V1.DiceKept k
        ? new GameEvents.V2.DiceKept(k.PlayerId, k.Dice, k.TableCenter, k.NewTurnScore, GameStage.Rolling)
        : e));

  private void Apply(IEnumerable<object> events)
  {
    foreach (var e in events)
    {
      _changes.Add(e);
      State = GameState.Fold(State, e);
    }
  }
}
