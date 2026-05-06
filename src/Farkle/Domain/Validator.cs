namespace Farkle.Domain;

internal abstract class Validator
{
  public abstract ValidationResult IsSatisfied();

  public AndValidator And(Validator validator)
  {
    return new AndValidator(this, validator);
  }
}

internal class AndValidator : Validator
{
  private readonly Validator _left;
  private readonly Validator _right;

  public AndValidator(Validator left, Validator right)
  {
    _left  = left;
    _right = right;
  }

  public override ValidationResult IsSatisfied()
  {
    if (!_left.IsSatisfied())
    {
      return _left.IsSatisfied() with { IsValid = _left.IsSatisfied() };
    }

    if (_right.IsSatisfied())
    {
      return new ValidationResult(true, string.Empty);
    }

    return _right.IsSatisfied() with { IsValid = _right.IsSatisfied() };
  }
}
