using System.Linq;
using FluentAssertions;
using Xunit;
using static HotDice.ArchitectureTests.ArchitectureModel;

namespace HotDice.ArchitectureTests;

/// <summary>
/// Convention guardrail: concrete implementations of the core's outbound ports live in the
/// dedicated infrastructure project (the in-core no-op fallbacks are the only exception). The
/// read-model store port is gone post-cutover (ADR 0004 — the Inline GameState snapshot is the
/// read model), so the SignalR broadcaster is the remaining outbound port to guard.
/// </summary>
public class PortImplementationShould
{
  [Theory]
  [InlineData("HotDice.Application.IGameEventBroadcaster")]
  public void LiveInTheInfrastructureProject(string port)
  {
    // In-core no-op defaults (types named Null*) keep the module runnable without infrastructure,
    // so they are allowed to stay in the core assembly.
    var misplaced = ConcreteImplementationsOf(port)
      .Where(t => t.Assembly.Name != InfraAsm
                  && !(t.Assembly.Name == CoreAsm && t.Name.StartsWith("Null")))
      .Select(t => $"{t.FullName} (in {t.Assembly.Name})")
      .OrderBy(x => x)
      .ToList();

    misplaced.Should().BeEmpty($"implementations of {port} belong in HotDice.Infrastructure");
  }
}
