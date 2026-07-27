using HotDice.Domain.GameAggregate;

namespace HotDice.Features.SetDiceAside;

// The SetDiceAside slice's write-side input, built by the endpoint from the route + request body.
public record SetDiceAsideCommand(GameId GameId, PlayerId PlayerId, DieValue Die);
