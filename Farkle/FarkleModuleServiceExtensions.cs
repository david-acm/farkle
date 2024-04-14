using System.Reflection;
using Eventuous;
using Eventuous.EventStore;
using Farkle.Application;
using Farkle.Contracts;
using Farkle.Domain.GameAggregate;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ILogger=Serilog.ILogger;

namespace Farkle;

public static class FarkleModuleServiceExtensions
{
  public static IServiceCollection AddFarkleModuleServices(this IServiceCollection services,
    ConfigurationManager configuration,
    ILogger logger, List<Assembly> mediatrAssemblies)
  {
    mediatrAssemblies.Add(typeof(FarkleModuleServiceExtensions).Assembly);
    
    services.AddCommandService<GameService, Game>();
    services.AddAggregateStore<EsdbEventStore>();
    services.AddSingleton<IGameService, GameService>();
    
    // TODO: Use Guard clause instead
    // TODO: Use configuration instead. Check the best way to configure the cors url
    var esdbConnString = configuration.GetConnectionString("Esdb") ?? string.Empty;
    services.AddEventStoreClient(esdbConnString);
    logger.Information($"Using esdb connection string: {esdbConnString}");
    
    logger.Information("{Module} module services registered", "Farkle.Domain");

    return services;
  }

  public static WebApplication SetUpFarkleModule(this WebApplication app)
  {
    // app.UseBlazorFrameworkFiles();
    // app.UseStaticFiles();
    // app.MapFallbackToFile("index.html");

    TypeMap.RegisterKnownEventTypes();

    app.MapCommands();

    return app;
  }
  private static void MapCommands(this WebApplication app)
  {

    app.MapAggregateCommands<Game>()
      // .MapDiscoveredCommands<Game>()
      .MapCommand<HttpRequests.StartGameRequest, Command.StartGame>(
        (cmd, ctx)
          => new Command.StartGame(
            cmd.Id))
      .MapCommand<HttpRequests.JoinPlayerRequest, Command.JoinPlayer>(
        (cmd, ctx)
          => new Command.JoinPlayer(
            cmd.GameId,
            cmd.PlayerId,
            cmd.PlayerName))
      .MapCommand<HttpRequests.RollDiceRequest, Command.RollDice>(
        (cmd, ctx)
          => new Command.RollDice(
            cmd.GameId,
            cmd.PlayerId))
      .MapCommand<HttpRequests.KeepDiceRequest, Command.KeepDice>(
        (cmd, ctx)
          => new Command.KeepDice(
            cmd.GameId,
            cmd.PlayerId,
            cmd.DiceValues.ToDiceValues()))
      .MapCommand<HttpRequests.PassTurnHttp, Command.PassTurn>(
        (cmd, ctx)
          => new Command.PassTurn(
            cmd.GameId,
            cmd.PlayerId));
  }
}
