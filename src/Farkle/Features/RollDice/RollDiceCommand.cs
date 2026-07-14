using Farkle.Domain.GameAggregate;

namespace Farkle.Features.RollDice;

// The RollDice slice's write-side input, built by the endpoint from the route.
public record RollDiceCommand(GameId GameId, PlayerId PlayerId);
