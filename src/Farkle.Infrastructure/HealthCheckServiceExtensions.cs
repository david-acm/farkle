using Farkle.Infrastructure.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Farkle.Infrastructure;

/// <summary>Readiness check for the backing service Farkle.Infrastructure owns (Identity Postgres).
/// Marten shares that Postgres, so the single Postgres check covers the event store too (ADR 0004);
/// the ESDB check is gone.</summary>
public static class HealthCheckServiceExtensions
{
  public static IServiceCollection AddFarkleHealthChecks(this IServiceCollection services)
  {
    services.AddHealthChecks()
      .AddDbContextCheck<AppDbContext>("postgres", tags: ["ready"]);
    return services;
  }
}
