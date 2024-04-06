using Eventuous;

namespace Farkle;

public class PreconditionsFailedException : DomainException
{
  public PreconditionsFailedException(string reason, object @event) : base(reason)
  {
    Event = @event;
  }

  public object Event { get; }
}
