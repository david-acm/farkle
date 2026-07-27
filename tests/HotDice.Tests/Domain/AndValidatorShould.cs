using HotDice.Domain;
using FluentAssertions;
using Moq;

namespace HotDice.Tests.Domain;

public class AndValidatorShould
{
  private static ValidationResult Pass() => new(true, string.Empty);
  private static ValidationResult Fail(object ev) => new(false, ev);

  private static Mock<Validator> MockValidator(ValidationResult result)
  {
    var mock = new Mock<Validator>();
    mock.Setup(v => v.IsSatisfied()).Returns(result);
    return mock;
  }

  [Fact]
  public void ReturnTrueWhenBothSidesPass()
  {
    var left  = MockValidator(Pass());
    var right = MockValidator(Pass());

    var result = left.Object.And(right.Object).IsSatisfied();

    result.IsValid.Should().BeTrue();
  }

  [Fact]
  public void ReturnLeftFailureWhenLeftFails()
  {
    var failEvent = new object();
    var left  = MockValidator(Fail(failEvent));
    var right = MockValidator(Pass());

    var result = left.Object.And(right.Object).IsSatisfied();

    result.IsValid.Should().BeFalse();
    result.FailedValidationEvent.Should().Be(failEvent);
  }

  [Fact]
  public void ReturnRightFailureWhenOnlyRightFails()
  {
    var failEvent = new object();
    var left  = MockValidator(Pass());
    var right = MockValidator(Fail(failEvent));

    var result = left.Object.And(right.Object).IsSatisfied();

    result.IsValid.Should().BeFalse();
    result.FailedValidationEvent.Should().Be(failEvent);
  }

  [Fact]
  public void NotEvaluateRightWhenLeftFails()
  {
    var left  = MockValidator(Fail(new object()));
    var right = MockValidator(Pass());

    left.Object.And(right.Object).IsSatisfied();

    right.Verify(v => v.IsSatisfied(), Times.Never);
  }

  [Fact]
  public void EvaluateEachSideExactlyOnce()
  {
    var left  = MockValidator(Pass());
    var right = MockValidator(Pass());

    left.Object.And(right.Object).IsSatisfied();

    left.Verify(v => v.IsSatisfied(), Times.Once);
    right.Verify(v => v.IsSatisfied(), Times.Once);
  }
}
