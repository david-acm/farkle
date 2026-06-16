using Microsoft.Extensions.DependencyInjection;

namespace Blazor.Dice;

public static class ServiceCollectionExtensions
{
  /// <summary>Registers the dice components' services (the rotation calculator).</summary>
  public static IServiceCollection AddBlazorDice(this IServiceCollection services)
  {
    services.AddSingleton<IRotationCalculator, RotationCalculator>();
    return services;
  }
}
