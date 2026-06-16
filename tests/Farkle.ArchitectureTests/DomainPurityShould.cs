using System.Linq;
using ArchUnitNET.Domain;
using FluentAssertions;
using Xunit;
using static Farkle.ArchitectureTests.ArchitectureModel;

namespace Farkle.ArchitectureTests;

/// <summary>
/// Domain-purity guardrails (strengthened replacement for the old, effectively-vacuous
/// Farkle.Tests/DomainClassesShould.cs): the innermost domain must not reach outward and stays
/// encapsulated (internal).
/// </summary>
public class DomainPurityShould
{
  [Fact]
  public void NotDependOnApplicationEndpointsWebOrInfrastructure()
  {
    ForbiddenDependencies(IsDomain,
        "Farkle.Application", "Farkle.Endpoints",
        "EventStore.Client", "Eventuous.EventStore", "Npgsql",
        "Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore", "FastEndpoints",
        InfraAsm, HostAsm)
      .Should().BeEmpty("domain types are the innermost layer — they must not depend on application, endpoints, web frameworks, or infrastructure");
  }

  [Fact]
  public void KeepDomainTypesInternal()
  {
    var publicDomainTypes = Model.Types
      .Where(IsDomain)
      .Where(t => t.Visibility == Visibility.Public)
      .Select(t => t.FullName)
      .OrderBy(x => x)
      .ToList();

    publicDomainTypes.Should().BeEmpty("domain types must stay internal to the Farkle assembly (encapsulation)");
  }
}
