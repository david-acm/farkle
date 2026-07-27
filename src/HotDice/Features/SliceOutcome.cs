using HotDice.Domain.GameAggregate;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Wolverine.Marten;

namespace HotDice.Features;

/// <summary>
/// Centralizes the validation-as-events → HTTP contract every mutating slice shares (ADR 0004). The
/// pure decider stays the single source of truth for the rules; this just removes the identical
/// error→400 / append / notify plumbing that was copy-pasted into all seven slice endpoints.
///
/// Given the loaded aggregate, the events a decider produced, a success mapper, and the broadcast
/// notification: if the decider emitted an <see cref="IErrorEvent"/>, surface the first one as a 400
/// ProblemDetails and append nothing (and broadcast nothing); otherwise append the events, fold them
/// onto the loaded state, map the 200 response, and cascade the notification for the outbox to publish.
/// </summary>
public static class SliceOutcome
{
  public static (Results<Ok<TResponse>, ProblemHttpResult>, Events, TNotification?) From<TResponse, TNotification>(
      GameState state,
      IEnumerable<object> decided,
      Func<GameState, TResponse> map,
      TNotification notification)
    where TResponse : notnull
    where TNotification : class
  {
    var events = decided as object[] ?? decided.ToArray();

    if (events.OfType<IErrorEvent>().FirstOrDefault() is { } error)
      return (TypedResults.Problem(statusCode: 400, title: error.GetType().Name), new Events(), null);

    return (TypedResults.Ok(map(GameState.Fold(state, events))), new Events(events), notification);
  }
}
