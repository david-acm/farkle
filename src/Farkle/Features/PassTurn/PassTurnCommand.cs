using Farkle.Domain.GameAggregate;

namespace Farkle.Features.PassTurn;

// The PassTurn slice's write-side input, built by the endpoint from the route.
public record PassTurnCommand(GameId GameId, PlayerId PlayerId);
