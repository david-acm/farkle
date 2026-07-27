using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace HotDice.Infrastructure.Identity;

/// <summary>
/// Wires ASP.NET Identity persistence (the <see cref="AppDbContext"/> on Postgres + the Identity
/// stores). The auth endpoints and JWT issuance stay in the host (a web concern); this owns only
/// the persistence/data-source decision.
/// </summary>
public static class IdentityServiceExtensions
{
  /// <summary>
  /// Registers the Identity DbContext + stores and returns the Npgsql data source that was built
  /// (non-null only in Azure/Entra mode), so the caller can reuse it for the read model — both
  /// share one Postgres database.
  /// </summary>
  public static NpgsqlDataSource? AddHotDiceIdentity(
    this IServiceCollection services, string? connectionString)
  {
    // Use Entra (managed-identity) auth only when there is a host but no password — i.e. Azure.
    // Local dev + the Testcontainers integration tests supply a password and keep plain password
    // auth; when the connection string is unset, defer to EF / the test's own override.
    var dataSource = !string.IsNullOrEmpty(connectionString)
                     && string.IsNullOrEmpty(new NpgsqlConnectionStringBuilder(connectionString).Password)
      ? IdentityDataSource.BuildEntra(connectionString)
      : null;

    services.AddDbContext<AppDbContext>(o =>
    {
      if (dataSource is not null)
        o.UseNpgsql(dataSource);
      else
        o.UseNpgsql(connectionString);
    });

    services
      .AddIdentityCore<AppUser>()
      .AddRoles<IdentityRole>()
      .AddEntityFrameworkStores<AppDbContext>()
      .AddSignInManager()
      .AddDefaultTokenProviders();

    return dataSource;
  }
}
