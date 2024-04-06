using Farkle.GameAggregate;
using FluentAssertions;

namespace Farkle.Tests.Domain;

public class ConstructorShould
{
  [Fact]
  public void FallBackToDefaultRandomProviderWhenNoneIsInjected()
  {
    // Arrange
    var sut = () => new Game();

    // Act
    sut.Should().NotThrow();
  }
}
