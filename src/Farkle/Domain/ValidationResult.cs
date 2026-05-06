namespace Farkle.Domain;

internal record ValidationResult(bool IsValid, object FailedValidationEvent)
{
  public static implicit operator bool(ValidationResult result)
  {
    return result.IsValid;
  }
}
