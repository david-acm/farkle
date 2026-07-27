using FluentAssertions;
using Xunit;

namespace Farkle.SpaTests;

public class AppVersionShould
{
    [Fact]
    public void ReturnANonEmptyVersion() =>
        Farkle.Ui.AppVersion.Current.Should().NotBeNullOrWhiteSpace();
}
