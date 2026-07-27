using HotDice.Domain.GameAggregate;
using FluentAssertions;

namespace HotDice.Tests.Domain;

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
