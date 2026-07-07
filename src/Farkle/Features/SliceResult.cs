using Ardalis.Result;
using Farkle.Domain.GameAggregate;
using Wolverine.Marten;

namespace Farkle.Features;

// Shared shape for the slice handlers (ADR 0004, Option A): given the pre-command aggregate and the
// events a decider produced, append them (error events included — stored facts, inert on replay) and
// return an Ardalis Result. An IErrorEvent → Result.Error (the endpoint maps it to HTTP 400); success
// maps the post-command state (folded locally) to the response DTO. Because the handler never throws,
// Wolverine's AutoApplyTransactions commits the append in both cases.
internal static class SliceResult
{
  public static (Result<T>, Events) From<T>(GameState state, object[] events, Func<GameState, T> map)
  {
    var wrapped = new Events();
    wrapped.AddRange(events);

    var error = events.OfType<IErrorEvent>().FirstOrDefault();
    return error is not null
      ? (Result<T>.Error(error.GetType().Name), wrapped)
      : (Result<T>.Success(map(GameState.Fold(state, events))), wrapped);
  }
}
