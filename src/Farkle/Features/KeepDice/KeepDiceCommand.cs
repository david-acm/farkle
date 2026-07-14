using Farkle.Domain.GameAggregate;

namespace Farkle.Features.KeepDice;

// The KeepDice slice's write-side input, built by the endpoint from the route + request body.
public record KeepDiceCommand(GameId GameId, PlayerId PlayerId, IEnumerable<DieValue> DiceValues);
