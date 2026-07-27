using FluentAssertions;
using Xunit;
using static HotDice.ArchitectureTests.ArchitectureModel;

namespace HotDice.ArchitectureTests;

/// <summary>
/// Domain-purity guardrails (strengthened replacement for the old, effectively-vacuous
/// HotDice.Tests/DomainClassesShould.cs): the innermost domain must not reach outward toward the
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
        "HotDice.Application",
        "Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore", "FastEndpoints",
        InfraAsm, HostAsm)
      .Should().BeEmpty("domain types are the innermost layer — they must not depend on application, web frameworks, or infrastructure");
  }

  [Fact]
  public void NotDependOnTheSlices()
  {
    // #329 moved each command out of the shared kernel and into the slice that owns it, which left
    // the domain referencing no slice at all. Nothing was stopping that from regressing: the rule
    // above forbids Application/web/infra but says nothing about HotDice.Features, so a domain type
    // reaching back into a slice would have stayed green.
    //
    // This is also the reason the *events* stay in the shared kernel rather than following the
    // commands into the slices: GameState folds every event, so a slice-local event would force
    // HotDice.Domain -> HotDice.Features and fail right here.
    ForbiddenDependencies(IsDomain, "HotDice.Features")
      .Should().BeEmpty("the domain is the innermost layer — slices depend on it, never the other way round");
  }
}
