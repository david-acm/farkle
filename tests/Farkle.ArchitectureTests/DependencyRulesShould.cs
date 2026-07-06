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
  // Infrastructure libraries the core (Farkle) must NOT compile against once the dedicated
  // Farkle.Infrastructure project owns them. Note: the Eventuous *abstractions*
  // (Eventuous, Eventuous.Application/Persistence/Subscriptions) deliberately stay in the core,
  // so they are NOT listed here — only the concrete event-store transport and the
  // database/realtime/identity stacks.
  private static readonly string[] InfrastructureLibraries =
  [
    "EventStore.Client",
    "Eventuous.EventStore",
    "Npgsql",
    "Microsoft.EntityFrameworkCore",
    "Microsoft.AspNetCore.SignalR",
    "Microsoft.AspNetCore.Identity",
  ];

  [Fact]
  public void KeepTheCoreFreeOfInfrastructureLibraries()
  {
    ForbiddenDependencies(CoreAsm, InfrastructureLibraries)
      .Should().BeEmpty("the Farkle core must not depend on the event store / EF / SignalR / Identity stacks — those belong in Farkle.Infrastructure");
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
  public void KeepTheBlazorClientOffTheServerCoreAndInfrastructure()
  {
    // The WASM client may use Farkle.Contracts / Farkle.SharedKernel / Farkle.ApiClient only.
    ForbiddenDependencies(ClientAsm,
        "Farkle.Domain", "Farkle.Application", "Farkle.Endpoints", InfraAsm)
      .Should().BeEmpty("the Blazor client must talk to the server over the API client + contracts, not the server core/infrastructure");
  }
}
