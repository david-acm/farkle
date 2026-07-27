using FluentValidation;
using static HotDice.Contracts.HttpRequests;

namespace HotDice.Features.KeepDice;

// Input validation for the KeepDice slice (#302 follow-up). Every value in the kept set must be a
// real die face (1..6) — each is mapped through DieValue.FromValue, which throws (and would leak a
// 500) on anything else. A null collection is rejected up front. The Wolverine.HTTP FluentValidation
// middleware turns a failure into a 400 ProblemDetails before the endpoint runs.
public sealed class KeepDiceRequestValidator : AbstractValidator<KeepDiceRequest>
{
  public KeepDiceRequestValidator()
  {
    RuleFor(x => x.DiceValues).NotNull();
    RuleForEach(x => x.DiceValues).InclusiveBetween(1, 6);
  }
}
