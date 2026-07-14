namespace Farkle.Domain.GameAggregate;

// Marker for validation-as-events: a decider returns one of these instead of throwing, and the slice
// endpoint maps the first one to a 400 ProblemDetails and appends nothing. Error events deliberately
// have no GameState.Apply overload — they are recorded as facts but inert on replay.
public interface IErrorEvent
{
}
