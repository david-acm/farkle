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
  // Infrastructure libraries the core (Farkle) must NOT compile against. Note (ADR 0004): the core
  // IS now Marten-native — it depends on Marten + Wolverine (and, transitively, Npgsql), so those
  // are deliberately absent. Note (ADR 0005): the core also broadcasts over SignalR directly
  // (IHubContext<GameHub>), so SignalR left this list too — the IGameEventBroadcaster port was
  // ceremony. EF Core and Identity (the auth stack) still stay out of the core.
  private static readonly string[] InfrastructureLibraries =
  [
    "Microsoft.EntityFrameworkCore",
    "Microsoft.AspNetCore.Identity",
  ];

  [Fact]
  public void KeepTheCoreFreeOfInfrastructureLibraries()
  {
    ForbiddenDependencies(CoreAsm, InfrastructureLibraries)
      .Should().BeEmpty("the Farkle core must not depend on the EF / Identity (auth) stacks — those belong in Farkle.Infrastructure");
  }

  [Fact]
  public void KeepFarkleSharedFreeOfTheWebFramework()
  {
    // #303 — the vertical slices own their Wolverine.HTTP endpoints, so the Farkle core compiles
    // against the web framework (Wolverine.Http + ASP.NET). The guardrail generalizes: Farkle.Shared
    // (the merged Contracts + SharedKernel leaf) must stay web-framework-free — it is shared with the
    // WASM client. Deciders staying pure is covered separately by KeepDecidersPureAndFrameworkFree.
    ForbiddenDependencies(SharedAsm, "FastEndpoints", "Wolverine", "Microsoft.AspNetCore")
      .Should().BeEmpty("Farkle.Shared must not depend on any web framework — it is shared with the Blazor client");
  }

  [Fact]
  public void KeepTheCoreFreeOfInfrastructureAndHost()
  {
    ForbiddenDependencies(CoreAsm, InfraAsm, HostAsm)
      .Should().BeEmpty("the core must not depend on Farkle.Infrastructure or the WebApp host (dependencies point inward)");
  }

  [Fact]
  public void KeepFarkleSharedAsAPureDependencyFreeLeaf()
  {
    // Farkle.Shared (the merged Contracts + SharedKernel) is the pure leaf shared by the server core
    // and the WASM client: it must not depend on any other Farkle project or infrastructure library.
    ForbiddenDependencies(SharedAsm,
        [.. InfrastructureLibraries,
         "Farkle.Domain", "Farkle.Application",
         InfraAsm, HostAsm])
      .Should().BeEmpty("Farkle.Shared is a pure, infra-free leaf — it must not depend on other Farkle projects");
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
  public void KeepSlicesOffTheInfrastructureAndHostLayers()
  {
    // #303 — a slice is now the complete use case: command + decider + Wolverine.HTTP endpoint +
    // response. The endpoint may use application ports (IGameCreator, IFeedbackWriter, GameNotifier),
    // so the slice→application edge is allowed. It must still not reach into the infrastructure or
    // host projects.
    ForbiddenDependencies(IsFeatureSlice, InfraAsm, HostAsm)
      .Should().BeEmpty("vertical slices may front the application layer but must not depend on infrastructure or the host");
  }

  [Fact]
  public void KeepTheBlazorClientOffTheServerCoreAndInfrastructure()
  {
    // The WASM client may use Farkle.Contracts / Farkle.SharedKernel / Farkle.ApiClient only.
    ForbiddenDependencies(ClientAsm,
        "Farkle.Domain", "Farkle.Application", InfraAsm)
      .Should().BeEmpty("the Blazor client must talk to the server over the API client + contracts, not the server core/infrastructure");
  }
}
