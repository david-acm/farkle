using Farkle.Application;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Farkle.WebTests.Harness;

// A deterministic id generator that yields a scripted sequence so the collision-retry path in
// GameCreator.CreateGameAsync can be exercised end to end.
public sealed class ScriptedGameIdGenerator(params int[] ids) : IGameIdGenerator
{
  private readonly Queue<int> _ids = new(ids);
  public int Next() => _ids.Dequeue();
}

// AppFixture variant that overrides IGameIdGenerator with the sequence [424242, 424242, 424243]: the
// second create draws 424242 again (the Marten stream already exists) and must retry, landing on
// 424243. Runs in its own Marten schema so it never collides with the main integration host's data.
public sealed class ScriptedIdAppFixture : AppFixture
{
  protected override string MartenSchema => "farkle_it_scripted";

  protected override void ConfigureTestServices(IServiceCollection services)
  {
    services.RemoveAll<IGameIdGenerator>();
    services.AddSingleton<IGameIdGenerator>(new ScriptedGameIdGenerator(424242, 424242, 424243));
  }
}
