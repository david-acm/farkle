using System.Reflection;
using Farkle.Application;
using Farkle.Domain.GameAggregate;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ILogger=Serilog.ILogger;

namespace Farkle;

public static class FarkleModuleServiceExtensions
{
  public static IServiceCollection AddFarkleModuleServices(this IServiceCollection services,
    ConfigurationManager configuration,
    ILogger logger, List<Assembly> mediatrAssemblies)
  {
    mediatrAssemblies.Add(typeof(FarkleModuleServiceExtensions).Assembly);

    // The command path is now Wolverine [AggregateHandler]s over Marten (AddFarkleCritterStack),
    // not an Eventuous CommandService (ADR 0004).
    services.AddSingleton<IGameIdGenerator, RandomGameIdGenerator>();
    services.AddSingleton<IGameCreator, GameCreator>();
    // Dice source seam (#93): the default RNG, resolved by Wolverine's generated handler code into
    // the RollDice handler. Hosts/tests can replace it (e.g. a deterministic ScriptedRandom).
    services.AddSingleton<IRandom, DefaultRandomProvider>();

    // Post-commit SignalR broadcast (ADR 0004): the endpoints call this after a committed command
    // to push the up-to-date GameState snapshot. Scoped — it uses Marten's scoped IQuerySession.
    services.AddScoped<GameNotifier>();

    // #277 — thin append-only feedback writer (no aggregate), now appending to a Marten stream.
    services.AddSingleton<IFeedbackWriter, FeedbackWriter>();

    // The write/read store (Marten) + command bus (Wolverine) are wired by the host via
    // AddFarkleCritterStack; SignalR delivery via AddFarkleRealtime.

    logger.Information("{Module} module services registered", "Farkle.Domain");

    return services;
  }

  public static WebApplication SetUpFarkleModule(this WebApplication app)
  {
    // Marten registers event types by CLR type (greenfield data, ADR 0002/0004), so the Eventuous
    // TypeMap.RegisterKnownEventTypes() bootstrap is gone.
    return app;
  }
}
