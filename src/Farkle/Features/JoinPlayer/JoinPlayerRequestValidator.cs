using FluentValidation;
using static Farkle.Contracts.HttpRequests;

namespace Farkle.Features.JoinPlayer;

// Input validation for the JoinPlayer slice (#302 follow-up). A player must have a non-blank name
// (NotEmpty treats a whitespace-only string as empty for strings), so an empty name no longer
// silently joins a nameless player. The Wolverine.HTTP FluentValidation middleware turns a failure
// into a 400 ProblemDetails before the endpoint runs.
public sealed class JoinPlayerRequestValidator : AbstractValidator<JoinPlayerRequest>
{
  public JoinPlayerRequestValidator() =>
    RuleFor(x => x.PlayerName).NotEmpty();
}
