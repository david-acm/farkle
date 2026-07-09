using FluentAssertions;
using Xunit;
using static Farkle.ArchitectureTests.ArchitectureModel;

namespace Farkle.ArchitectureTests;

/// <summary>
/// Solution-organization guardrails: the dependency rule (dependencies point inward) and
/// infrastructure isolation. These encode the target architecture for the
/// Farkle.Infrastructure extraction and protect it from regressing.
/// </summary>
public class DependencyRulesShould
{
  // Infrastructure libraries the core (Farkle) must NOT compile against — the realtime/identity/EF
  // stacks belong in Farkle.Infrastructure. Note (ADR 0004): the core IS now Marten-native — it
  // depends on Marten + Wolverine (and, transitively, Npgsql for the collision-retry in
  // GameCreator), so those are deliberately absent from this list. EF Core, SignalR and Identity
  // still stay out of the core.
  private static readonly string[] InfrastructureLibraries =
  [
    "Microsoft.EntityFrameworkCore",
    "Microsoft.AspNetCore.SignalR",
    "Microsoft.AspNetCore.Identity",
  ];

  [Fact]
  public void KeepTheCoreFreeOfInfrastructureLibraries()
  {
    ForbiddenDependencies(CoreAsm, InfrastructureLibraries)
      .Should().BeEmpty("the Farkle core must not depend on the EF / SignalR / Identity stacks — those belong in Farkle.Infrastructure");
  }

  [Fact]
  public void KeepTheCoreFreeOfTheWebFramework()
  {
    // #292 — the HTTP endpoints moved to Farkle.Endpoints, so the domain/application core no
    // longer compiles against the web framework. Keep it that way.
    ForbiddenDependencies(CoreAsm, "FastEndpoints")
      .Should().BeEmpty("the Farkle core must not depend on the web framework (FastEndpoints) — that belongs in Farkle.Endpoints");
  }

  [Fact]
  public void KeepTheCoreFreeOfInfrastructureHostAndEndpointProjects()
  {
    ForbiddenDependencies(CoreAsm, InfraAsm, HostAsm, EndpointsAsm)
      .Should().BeEmpty("the core must not depend on Farkle.Infrastructure, Farkle.Endpoints or the WebApp host (dependencies point inward)");
  }

  [Fact]
  public void KeepContractsAsADependencyFreeLeaf()
  {
    ForbiddenDependencies(ContractsAsm,
        "Farkle.Domain", "Farkle.Application", "Farkle.Endpoints",
        SharedKernelAsm, InfraAsm, HostAsm)
      .Should().BeEmpty("Farkle.Contracts is a shared leaf — it must not depend on other Farkle projects");
  }

  [Fact]
  public void KeepTheSharedKernelPureAndDependencyFree()
  {
    ForbiddenDependencies(SharedKernelAsm,
        [.. InfrastructureLibraries,
         "Farkle.Domain", "Farkle.Application", "Farkle.Endpoints",
         ContractsAsm, InfraAsm, HostAsm])
      .Should().BeEmpty("Farkle.SharedKernel is a pure, infra-free leaf shared by server and client");
  }

  [Fact]
  public void KeepDecidersPureAndFrameworkFree()
  {
    // #301 — the heart of the vertical-slice model: each slice's Decide(command, state) -> events
    // function is pure. It may live in the (eventually framework-coupled) slice, but the decision
    // logic itself must never reach for a persistence / messaging / web framework. Enforced by
    // test rather than by an assembly boundary, so slices keep their locality.
    ForbiddenDependencies(IsDecider,
        "Eventuous", "Marten", "Wolverine", "Microsoft.AspNetCore", "FastEndpoints", "Npgsql")
      .Should().BeEmpty("slice deciders must stay pure — no framework in the decision logic");
  }

  [Fact]
  public void KeepSlicesOffTheApplicationAndInfrastructureLayers()
  {
    // #301 — a slice depends inward only (domain + shared kernel). It must not reach into the
    // application/host/infrastructure layers; cross-slice coupling goes through the shared kernel.
    ForbiddenDependencies(IsFeatureSlice,
        "Farkle.Application", InfraAsm, HostAsm, EndpointsAsm)
      .Should().BeEmpty("vertical slices point inward — no dependency on application/host/infra");
  }

  [Fact]
  public void KeepTheBlazorClientOffTheServerCoreAndInfrastructure()
  {
    // The WASM client may use Farkle.Contracts / Farkle.SharedKernel / Farkle.ApiClient only.
    ForbiddenDependencies(ClientAsm,
        "Farkle.Domain", "Farkle.Application", "Farkle.Endpoints", InfraAsm)
      .Should().BeEmpty("the Blazor client must talk to the server over the API client + contracts, not the server core/infrastructure");
  }
}
