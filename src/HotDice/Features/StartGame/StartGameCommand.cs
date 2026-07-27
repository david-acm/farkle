using HotDice.Domain.GameAggregate;

namespace HotDice.Features.StartGame;

// The StartGame slice's write-side input. Unlike the other slices, no endpoint constructs it:
// StartGame has no existing stream, so IGameCreator builds it and StartStreams the result.
public record StartGameCommand(GameId GameId);
