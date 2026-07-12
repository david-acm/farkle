using FluentAssertions;
using Xunit;
using static Farkle.ArchitectureTests.ArchitectureModel;

namespace Farkle.ArchitectureTests;

/// <summary>
/// Domain-purity guardrails (strengthened replacement for the old, effectively-vacuous
/// Farkle.Tests/DomainClassesShould.cs): the innermost domain must not reach outward toward the
/// EF / SignalR / web / host layers.
///
/// Encapsulation note (ADR 0004): the domain is now Marten-native — the events, aggregate snapshot
/// (<c>GameState</c>), commands and value objects are the persisted serialization contract Marten
/// reads/writes, so they are deliberately <em>public</em> (the standard Critter Stack convention).
/// The old "keep domain types internal" guardrail no longer applies; purity is enforced by the
/// outward-dependency rule below, not by visibility. Note Marten/Wolverine (and, transitively,
/// Npgsql) are permitted in the domain — that inward coupling is the whole point of going native.
/// </summary>
public class DomainPurityShould
{
  [Fact]
  public void NotDependOnApplicationWebOrInfrastructure()
  {
    ForbiddenDependencies(IsDomain,
        "Farkle.Application",
        "Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore", "FastEndpoints",
        InfraAsm, HostAsm)
      .Should().BeEmpty("domain types are the innermost layer — they must not depend on application, web frameworks, or infrastructure");
  }
}
