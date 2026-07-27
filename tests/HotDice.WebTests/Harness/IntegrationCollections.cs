namespace HotDice.WebTests.Harness;

// The main integration collection: every per-slice Alba test shares one IAlbaHost (booted once here),
// so the suite pays for a single host instead of one WebApplicationFactory per test class.
[CollectionDefinition(Name)]
public sealed class IntegrationCollection : ICollectionFixture<AppFixture>
{
  public const string Name = "integration";
}

// A separate collection for the scripted-id host — a distinct Program configuration (scripted
// IGameIdGenerator, own Marten schema) that would otherwise pollute the main host.
[CollectionDefinition(Name)]
public sealed class ScriptedIdCollection : ICollectionFixture<ScriptedIdAppFixture>
{
  public const string Name = "integration-scripted-id";
}
