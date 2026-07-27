using System.Reflection;
using HotDice.Application;
using HotDice.Domain.GameAggregate;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ILogger=Serilog.ILogger;

namespace HotDice;

public static class HotDiceModuleServiceExtensions
{
  public static IServiceCollection AddHotDiceModuleServices(this IServiceCollection services,
    ConfigurationManager configuration,
    ILogger logger, List<Assembly> mediatrAssemblies)
  {
    mediatrAssemblies.Add(typeof(HotDiceModuleServiceExtensions).Assembly);

    // The command path is now Wolverine [AggregateHandler]s over Marten (AddHotDiceCritterStack),
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

    // #302 follow-up — register every slice request validator (AbstractValidator<T>, public so the
    // FluentValidation HTTP policy wires them) in this assembly as an IValidator<T>. The host's
    // UseFluentValidationProblemDetailMiddleware picks these up at codegen time and injects a
    // 400-ProblemDetails guard ahead of the matching endpoint. Singleton (validators are stateless +
    // thread-safe) makes Wolverine inject IValidator<T> as a constructor field of the generated
    // handler — a deterministic position. A scoped registration instead nudges Wolverine to `new` the
    // concrete inline as a floating local, whose ordering drifts between environments (breaking
    // verify-codegen); the singleton field injection avoids that.
    services.AddValidatorsFromAssembly(
      typeof(HotDiceModuleServiceExtensions).Assembly, ServiceLifetime.Singleton);

    // The write/read store (Marten) + command bus (Wolverine) are wired by the host via
    // AddHotDiceCritterStack; SignalR delivery via AddSignalR + MapHub in the host.

    logger.Information("{Module} module services registered", "HotDice.Domain");

    return services;
  }

  public static WebApplication SetUpHotDiceModule(this WebApplication app)
  {
    // Marten registers event types by CLR type (greenfield data, ADR 0002/0004), so the Eventuous
    // TypeMap.RegisterKnownEventTypes() bootstrap is gone.
    return app;
  }
}
