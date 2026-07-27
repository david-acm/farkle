using FluentAssertions;
using Xunit;
using static HotDice.ArchitectureTests.ArchitectureModel;

namespace HotDice.ArchitectureTests;

/// <summary>
/// Solution-organization guardrails: the dependency rule (dependencies point inward) and
/// infrastructure isolation. These encode the target architecture for the
/// HotDice.Infrastructure extraction and protect it from regressing.
/// </summary>
public class DependencyRulesShould
{
  // Infrastructure libraries the core (HotDice) must NOT compile against. Note (ADR 0004): the core
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
      .Should().BeEmpty("the HotDice core must not depend on the EF / Identity (auth) stacks — those belong in HotDice.Infrastructure");
  }

  [Fact]
  public void KeepFarkleSharedFreeOfTheWebFramework()
  {
    // #303 — the vertical slices own their Wolverine.HTTP endpoints, so the HotDice core compiles
    // against the web framework (Wolverine.Http + ASP.NET). The guardrail generalizes: HotDice.Shared
    // (the merged Contracts + SharedKernel leaf) must stay web-framework-free — it is shared with the
    // WASM client. Deciders staying pure is covered separately by KeepDecidersPureAndFrameworkFree.
    ForbiddenDependencies(SharedAsm, "FastEndpoints", "Wolverine", "Microsoft.AspNetCore")
      .Should().BeEmpty("HotDice.Shared must not depend on any web framework — it is shared with the Blazor client");
  }

  [Fact]
  public void KeepTheCoreFreeOfInfrastructureAndHost()
  {
    ForbiddenDependencies(CoreAsm, InfraAsm, HostAsm)
      .Should().BeEmpty("the core must not depend on HotDice.Infrastructure or the WebApp host (dependencies point inward)");
  }

  [Fact]
  public void KeepFarkleSharedAsAPureDependencyFreeLeaf()
  {
    // HotDice.Shared (the merged Contracts + SharedKernel) is the pure leaf shared by the server core
    // and the WASM client: it must not depend on any other HotDice project or infrastructure library.
    ForbiddenDependencies(SharedAsm,
        [.. InfrastructureLibraries,
         "HotDice.Domain", "HotDice.Application",
         InfraAsm, HostAsm])
      .Should().BeEmpty("HotDice.Shared is a pure, infra-free leaf — it must not depend on other HotDice projects");
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
    // The client UI may use HotDice.Contracts / HotDice.SharedKernel / HotDice.ApiClient only.
    // Checked against HotDice.Ui, which is where the UI actually lives — the rule used to name
    // WebApp.Client, and kept passing vacuously once that became a bare WASM entry point (#348).
    ForbiddenDependencies(UiAsm,
        "HotDice.Domain", "HotDice.Application", InfraAsm)
      .Should().BeEmpty("the shared client UI must talk to the server over the API client + contracts, not the server core/infrastructure");
  }
}
