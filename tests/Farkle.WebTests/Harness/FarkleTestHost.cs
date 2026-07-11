using Alba;
using Farkle.Infrastructure.Identity;
using Marten;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Farkle.WebTests.Harness;

// Builds the shared integration host configuration in one place, so the collection fixtures and the
// throwaway host used by the codegen type-check test all boot the real Program the same way: the game
// endpoints authorized, both stores on the test Postgres (Marten in an isolated schema), and external
// Wolverine transports disabled so TrackedSession can await cascaded broadcasts in-process.
public static class FarkleTestHost
{
  public static Task<IAlbaHost> StartAsync(
    string connectionString, string martenSchema, Action<IServiceCollection>? configure = null) =>
    AlbaHost.For<Program>(builder =>
    {
      builder.ConfigureAppConfiguration((_, cfg) =>
        cfg.AddInMemoryCollection(new Dictionary<string, string?>
        {
          ["Auth:RequireAuthorization"] = "true",
        }));

      builder.ConfigureServices(services =>
      {
        // AddFarkleIdentity / AddFarkleCritterStack captured their connection at registration from an
        // unset config value, so point both stores at the test Postgres here.
        var dbDesc = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
        if (dbDesc != null) services.Remove(dbDesc);
        services.AddDbContext<AppDbContext>(o => o.UseNpgsql(connectionString));

        services.ConfigureMarten(opts =>
        {
          opts.Connection(connectionString);   // last Connection() wins
          opts.DatabaseSchemaName = martenSchema;
        });

        services.DisableAllExternalWolverineTransports();

        configure?.Invoke(services);
      });
    });

  // Racing the host stop with the Marten/Wolverine durability agent can surface a harmless
  // ObjectDisposedException (sometimes wrapped in an AggregateException) — swallow just that on
  // teardown; anything else propagates and fails the run.
  public static async Task StopTolerantlyAsync(IAlbaHost host)
  {
    try
    {
      await host.DisposeAsync();
    }
    catch (Exception ex) when (IsHarmlessTeardown(ex))
    {
      System.Diagnostics.Debug.WriteLine($"Ignoring harmless host-teardown exception: {ex.GetType().Name}");
    }
  }

  private static bool IsHarmlessTeardown(Exception ex) =>
    ex is ObjectDisposedException
    || (ex is AggregateException agg && agg.Flatten().InnerExceptions.All(e => e is ObjectDisposedException));
}
