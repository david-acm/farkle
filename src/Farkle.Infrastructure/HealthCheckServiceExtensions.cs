using Farkle.Infrastructure.Identity;
using Farkle.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Farkle.Infrastructure;

/// <summary>Readiness checks for the backing services Farkle.Infrastructure owns (Postgres + ESDB).</summary>
public static class HealthCheckServiceExtensions
{
  public static IServiceCollection AddFarkleHealthChecks(this IServiceCollection services)
  {
    services.AddHealthChecks()
      .AddDbContextCheck<AppDbContext>("postgres", tags: ["ready"])
      .AddCheck<EventStoreHealthCheck>("eventstore", tags: ["ready"]);
    return services;
  }
}
