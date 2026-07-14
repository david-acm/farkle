using Farkle.Domain.GameAggregate;

namespace Farkle.Features.ReturnDice;

// The ReturnDice slice's write-side input, built by the endpoint from the route + request body.
public record ReturnDiceCommand(GameId GameId, PlayerId PlayerId, DieValue Die);
