using System.Reflection;
using Eventuous;
using Eventuous.EventStore;
using Farkle.WebApi.Application;
using Farkle.GameAggregate;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ILogger=Serilog.ILogger;

namespace Farkle.WebApi;

public static class FarkleModuleServiceExtensions
{
  public static IServiceCollection AddFarkleModuleServices(this IServiceCollection services,
    ConfigurationManager configuration,
    ILogger logger, List<Assembly> mediatrAssemblies)
  {
    mediatrAssemblies.Add(typeof(FarkleModuleServiceExtensions).Assembly);
    
    services.AddCommandService<GameService, Game>();
    services.AddAggregateStore<EsdbEventStore>();
    
    // TODO: Use Guard clause instead
    // TODO: Use configuration instead. Check the best way to configure the cors url
    var esdbConnString = configuration.GetConnectionString("Esdb") ?? string.Empty;
    services.AddEventStoreClient(esdbConnString);
    logger.Information($"Using esdb connection string: {esdbConnString}");
    
    logger.Information("{Module} module services registered", "Farkle");

    return services;
  }

  public static WebApplication SetUpFarkleModule(this WebApplication app)
  {
    app.UseBlazorFrameworkFiles();
    app.UseStaticFiles();
    app.MapFallbackToFile("index.html");
    app.UseAuthorization();

    TypeMap.RegisterKnownEventTypes();

    app.MapCommands();

    return app;
  }
  private static void MapCommands(this WebApplication app)
  {

    app.MapAggregateCommands<Game>()
      // .MapDiscoveredCommands<Game>()
      .MapCommand<V1.StartGameHttp, Command.StartGame>(
        (cmd, ctx)
          => new Command.StartGame(
            cmd.Id))
      .MapCommand<V1.JoinPlayerHttp, Command.JoinPlayer>(
        (cmd, ctx)
          => new Command.JoinPlayer(
            cmd.GameId,
            cmd.PlayerId,
            cmd.PlayerName))
      .MapCommand<V1.RollDiceHttp, Command.RollDice>(
        (cmd, ctx)
          => new Command.RollDice(
            cmd.GameId,
            cmd.PlayerId))
      .MapCommand<V1.KeepDiceHttp, Command.KeepDice>(
        (cmd, ctx)
          => new Command.KeepDice(
            cmd.GameId,
            cmd.PlayerId,
            cmd.DiceValues.ToDiceValues()))
      .MapCommand<V1.PassTurnHttp, Command.PassTurn>(
        (cmd, ctx)
          => new Command.PassTurn(
            cmd.GameId,
            cmd.PlayerId));
  }
}
