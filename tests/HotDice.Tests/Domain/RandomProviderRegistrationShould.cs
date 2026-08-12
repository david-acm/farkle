using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HotDice;
using HotDice.Domain.GameAggregate;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HotDice.Tests.Domain;

// The dice-source seam (#93) is swapped for a deterministic provider when Dice:Scripted is set, so a
// black-box host (the device happy path, #339) gets a guaranteed scoring die. Production leaves the
// flag unset and keeps the real RNG.
public class RandomProviderRegistrationShould
{
  [Fact]
  public void UseTheDefaultProvider_WhenScriptedDiceIsNotConfigured()
  {
    RegisteredRandom(scripted: null).Should().Be(typeof(DefaultRandomProvider));
  }

  [Fact]
  public void UseTheScriptedProvider_WhenScriptedDiceIsEnabled()
  {
    RegisteredRandom(scripted: "true").Should().Be(typeof(ScriptedRandomProvider));
  }

  [Fact]
  public void ScriptedProviderAlwaysRollsTheMinimum_SoEveryRollScores()
  {
    // Next(1, 7) → 1 for every die: six 1s, so a roll always contains a scoring die.
    new ScriptedRandomProvider().Next(1, 7).Should().Be(1);
  }

  private static System.Type? RegisteredRandom(string? scripted)
  {
    using var configuration = new ConfigurationManager();
    if (scripted is not null)
      configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Dice:Scripted"] = scripted });

    using var logger = new Serilog.LoggerConfiguration().CreateLogger();
    var services = new ServiceCollection();
    services.AddHotDiceModuleServices(configuration, logger, new List<Assembly>());

    return services.Single(d => d.ServiceType == typeof(IRandom)).ImplementationType;
  }
}
