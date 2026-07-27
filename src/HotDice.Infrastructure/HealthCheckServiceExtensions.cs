using HotDice.Infrastructure.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace HotDice.Infrastructure;

/// <summary>Readiness check for the backing service HotDice.Infrastructure owns (Identity Postgres).
/// Marten shares that Postgres, so the single Postgres check covers the event store too (ADR 0004);
/// the ESDB check is gone.</summary>
public static class HealthCheckServiceExtensions
{
  public static IServiceCollection AddHotDiceHealthChecks(this IServiceCollection services)
  {
    services.AddHealthChecks()
      .AddDbContextCheck<AppDbContext>("postgres", tags: ["ready"]);
    return services;
  }
}
