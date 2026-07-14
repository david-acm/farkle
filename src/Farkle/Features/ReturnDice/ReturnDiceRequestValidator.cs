using FluentValidation;
using static Farkle.Contracts.HttpRequests;

namespace Farkle.Features.ReturnDice;

// Input validation for the ReturnDice slice (#302 follow-up). A die value must be a real face (1..6);
// anything else throws in DieValue.FromValue and would leak a 500. The Wolverine.HTTP FluentValidation
// middleware turns a failure into a 400 ProblemDetails before the endpoint runs.
public sealed class ReturnDiceRequestValidator : AbstractValidator<ReturnDiceRequest>
{
  public ReturnDiceRequestValidator() =>
    RuleFor(x => x.DieValue).InclusiveBetween(1, 6);
}
