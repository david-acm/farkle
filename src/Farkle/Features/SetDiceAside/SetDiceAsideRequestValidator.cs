using FluentValidation;
using static Farkle.Contracts.HttpRequests;

namespace Farkle.Features.SetDiceAside;

// Input validation for the SetDiceAside slice (#302 follow-up). A die value must be a real face
// (1..6); anything else throws in DieValue.FromValue and would leak a 500. The Wolverine.HTTP
// FluentValidation middleware turns a failure into a 400 ProblemDetails before the endpoint runs.
public sealed class SetDiceAsideRequestValidator : AbstractValidator<SetDiceAsideRequest>
{
  public SetDiceAsideRequestValidator() =>
    RuleFor(x => x.DieValue).InclusiveBetween(1, 6);
}
