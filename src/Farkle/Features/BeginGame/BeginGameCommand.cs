using Farkle.Domain.GameAggregate;

namespace Farkle.Features.BeginGame;

// The BeginGame slice's write-side input, built by the endpoint from the route + request body.
public record BeginGameCommand(GameId GameId, PlayerId PlayerId);
