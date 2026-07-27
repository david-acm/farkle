using HotDice.Domain.GameAggregate;

namespace HotDice.Features.JoinPlayer;

// The JoinPlayer slice's write-side input, built by the endpoint from the route + request body.
public record JoinPlayerCommand(int GameId, string Name);
