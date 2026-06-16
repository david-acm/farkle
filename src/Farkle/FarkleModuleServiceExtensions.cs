using System.Reflection;
using Eventuous;
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

    services.AddCommandService<GameService, GameState>();
    services.AddSingleton<IGameService, GameService>();
    services.AddSingleton<IGameIdGenerator, RandomGameIdGenerator>();
    services.AddSingleton<IGameCreator, GameCreator>();

    // Read-side (#156): default to the no-op store so GetGameStateEndpoint always resolves
    // IGameViewStore and falls back to replay. Farkle.Infrastructure registers the real
    // EfGameViewStore + the projector subscription when the read model is enabled.
    services.TryAddSingleton<IGameViewStore, NullGameViewStore>();

    // The concrete event-store transport (ESDB client, aggregate store, broadcast subscription)
    // lives in Farkle.Infrastructure and is wired by the host via AddFarkleEventStore — the core
    // depends only on Eventuous' IAggregateStore abstraction (resolved at runtime).

    logger.Information("{Module} module services registered", "Farkle.Domain");

    return services;
  }

  public static WebApplication SetUpFarkleModule(this WebApplication app)
  {
    TypeMap.RegisterKnownEventTypes();

    // app.MapCommands(); // Commented out to prevent duplicate routes with FastEndpoints

    return app;
  }
}
