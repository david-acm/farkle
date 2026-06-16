using System.Linq;
using FluentAssertions;
using Xunit;
using static Farkle.ArchitectureTests.ArchitectureModel;

namespace Farkle.ArchitectureTests;

/// <summary>
/// Convention guardrail: concrete implementations of the core's outbound ports live in the
/// dedicated infrastructure project (the in-core no-op fallbacks are the only exception). This is
/// RED until the read-model store and the SignalR broadcaster move into Farkle.Infrastructure.
/// </summary>
public class PortImplementationShould
{
  [Theory]
  [InlineData("Farkle.Application.IGameViewStore")]
  [InlineData("Farkle.Application.IGameEventBroadcaster")]
  public void LiveInTheInfrastructureProject(string port)
  {
    // The in-core no-op defaults (e.g. NullGameViewStore) keep the module runnable without
    // infrastructure, so they are allowed to stay in the core assembly.
    var misplaced = ConcreteImplementationsOf(port)
      .Where(t => t.Assembly.Name != InfraAsm
                  && !(t.Assembly.Name == CoreAsm && t.Name.StartsWith("Null")))
      .Select(t => $"{t.FullName} (in {t.Assembly.Name})")
      .OrderBy(x => x)
      .ToList();

    misplaced.Should().BeEmpty($"implementations of {port} belong in Farkle.Infrastructure");
  }
}
