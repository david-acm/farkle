using FluentAssertions;
using Xunit;

namespace HotDice.SpaTests;

public class AppVersionShould
{
    [Fact]
    public void ReturnANonEmptyVersion() =>
        HotDice.Ui.AppVersion.Current.Should().NotBeNullOrWhiteSpace();
}
