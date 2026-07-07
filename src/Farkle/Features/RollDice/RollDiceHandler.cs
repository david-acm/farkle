using Ardalis.Result;
using Farkle.Domain.GameAggregate;
using Wolverine.Marten;
using static Farkle.Contracts.HttpResponses;

namespace Farkle.Features.RollDice;

// RollDice slice handler (ADR 0004). Wolverine's [AggregateHandler] loads the GameState stream by the
// command's computed Id ("game-{code}"), rolls the dice (the IRandom side effect), calls the pure
// #301 decider, appends the events, and returns the response (or a 400-mapped error).
public static class RollDiceHandler
{
  [AggregateHandler]
  public static (Result<RollDiceResponse>, Events) Handle(Command.RollDice cmd, GameState state, IRandom rng)
  {
    var roll   = Dice.FromNewRoll(rng, state.DiceToRoll);
    var events = RollDiceDecider.Decide(cmd, state, roll).ToArray();
    return SliceResult.From(state, events,
      s => new RollDiceResponse(s.Code, s.TableCenter.Select(d => d.Value).ToArray()));
  }
}
