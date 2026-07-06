using System.Collections.Immutable;
using Eventuous;
using Farkle.SharedKernel.Scoring;
using Farkle.SharedKernel.Turns;
using V1 = Farkle.Domain.GameAggregate.GameEvents.V1;
using V2 = Farkle.Domain.GameAggregate.GameEvents.V2;

namespace Farkle.Domain.GameAggregate;

internal class Game : Aggregate<GameState>
{
  // A player wins once their banked total reaches this on a pass (#178).
  private const int WinningScore = 5_000;

  private readonly IRandom _randomProvider;

  public Game() : this(default!)
  {
  }

  public Game(IRandom? randomProvider)
  {
    _randomProvider = randomProvider ?? new DefaultRandomProvider();
  }

  public void Start(Command.StartGame startGame)
  {
    // Delegates to the pure StartGame decider (validation-as-events included). Applies its
    // output through base.Apply directly — the decider already validated, so the legacy
    // GameValidator path is bypassed for converted slices.
    foreach (var @event in Features.StartGame.StartGameDecider.Decide(startGame, State))
      base.Apply(@event);
  }

  public void JoinPlayer(Command.JoinPlayer joinPlayer)
  {
    foreach (var @event in Features.JoinPlayer.JoinPlayerDecider.Decide(joinPlayer, State))
      base.Apply(@event);
  }

  public void BeginGame(Command.BeginGame beginGame)
  {
    foreach (var @event in Features.BeginGame.BeginGameDecider.Decide(beginGame, State))
      base.Apply(@event);
  }

  public void RollDiceV1(Command.RollDice rollDice)
  {
    var roll = Dice.FromNewRoll(
      _randomProvider,
      GetNumberOfDiceToTrow());

    Apply(new GameEvents.V1.DiceRolled(
      rollDice.PlayerId,
      roll.DiceValues.ToPrimitiveArray(),
      GetScoreAfterRoll(roll)));
  }

  public void RollDiceV2(Command.RollDice rollDice)
  {
    // The roll is the side effect (IRandom); the pure decider takes the resulting dice.
    var roll = Dice.FromNewRoll(_randomProvider, State.DiceToRoll);

    foreach (var @event in Features.RollDice.RollDiceDecider.Decide(rollDice, State, roll))
      base.Apply(@event);
  }

  public void PassTurn(Command.PassTurn passTurn)
  {
    foreach (var @event in Features.PassTurn.PassTurnDecider.Decide(passTurn, State))
      base.Apply(@event);
  }

  public void KeepDice(Command.KeepDice keepDice)
  {
    foreach (var @event in Features.KeepDice.KeepDiceDecider.Decide(keepDice, State))
      base.Apply(@event);
  }

  public void KeepDiceV2(Command.KeepDice keepDice)
  {
    Apply(new V2.DiceKept(keepDice.PlayerId, keepDice.DiceValues.ToPrimitiveArray(),
      GetTableCenterDice(keepDice),
      GetNewTurnScore(keepDice.DiceValues, State.TurnScore, State),
      GameStage.Rolling));
  }

  // #159 — set aside / put back. Transient selection only: these never score or keep
  // dice (that stays Keep's job) and leave the table center untouched, so Keep continues
  // to validate against the full roll.
  public void SetDiceAside(Command.SetDiceAside cmd)
  {
    foreach (var @event in Features.SetDiceAside.SetDiceAsideDecider.Decide(cmd, State))
      base.Apply(@event);
  }

  public void ReturnDice(Command.ReturnDice cmd)
  {
    foreach (var @event in Features.ReturnDice.ReturnDiceDecider.Decide(cmd, State))
      base.Apply(@event);
  }

  private int[] GetTableCenterDice(Command.KeepDice keepDice)
  {
    var tableCenter = State.TableCenter.RemoveRange(keepDice.DiceValues);
    if (!tableCenter.IsEmpty)
    {
      return tableCenter.ToPrimitiveArray();
    }

    return State.TableCenter.AddRange(keepDice.DiceValues).ToPrimitiveArray();
  }

  private int GetScore(Command.PlayerId playerId)
  {
    return State.GameScoreFor(playerId) + State.TurnScore;
  }

  private ImmutableArray<Player> GetPlayerOrder(int playerId)
  {
    var player        = State.GetPlayer(playerId);
    var newPlayerList = State.Players.Remove(player).Add(player);
    return newPlayerList;
  }

  private void Apply(object @event)
  {
    // Validation-as-events: a command that breaks a precondition applies an IErrorEvent
    // instead of throwing; the application layer surfaces those as Ardalis.Result errors
    // (and the endpoints as HTTP), so domain rule violations never raise exceptions.
    var result = GameValidator.ValidatePreconditions(this, @event);

    if (result.IsValid)
    {
      base.Apply(@event);
      return;
    }

    base.Apply(result.FailedValidationEvent);
  }

  private int GetNumberOfDiceToTrow()
  {
    var kept = State.DiceKept.Length % 6;
    return 6 - kept;
  }

  private static int GetNewTurnScore(IEnumerable<DieValue> diceKept, int currentScore, GameState state)
  {
    // Scoring is now the shared, infra-free ScoreCalculator (single source of truth, reused
    // by the UI's live preview). The aggregate keeps only the turn-level combo rule: a second
    // four-of-a-kind ("straight") in the same turn doubles the running total.
    var breakdown = ScoreCalculator.Evaluate(diceKept.Select(d => d.Value).ToList());
    var turnScore = breakdown.Points;

    if (breakdown.Tricks.Contains(ScoringTrick.FourOfAKind) && state.StraightsKeptThisTurn > 0)
      return new Score((currentScore + turnScore) * 2);

    return new Score(currentScore + turnScore);
  }

  private Score GetScoreAfterRoll(Dice dice)
  {
    var canKeepAnyDice = new CanKeepDice(dice).IsSatisfied();

    return canKeepAnyDice ? State.TurnScore : new Score(0);
  }
}

// Color is the player's identity colour (a hex string), assigned by join order from
// PlayerColors. It is always derived from the player's id, so it is stable across event
// replays and snapshot round-trips even for events stored before colours existed.
internal record Player(int Id, string Name, string Color = "");
