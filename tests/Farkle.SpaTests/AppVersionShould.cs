using FluentAssertions;
using Xunit;

namespace Farkle.SpaTests;

public class AppVersionShould
{
    [Fact]
    public void ReturnANonEmptyVersion() =>
        WebApp.Client.AppVersion.Current.Should().NotBeNullOrWhiteSpace();
}
