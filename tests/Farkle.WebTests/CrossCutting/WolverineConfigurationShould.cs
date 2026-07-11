using Farkle.WebTests.Harness;
using JasperFx;

namespace Farkle.WebTests.CrossCutting;

// Configuration-as-a-test (#304). Runs the JasperFx "codegen test" command against the real host: it
// generates and compiles every Wolverine handler + Wolverine.HTTP endpoint, so a wiring or codegen
// error (an unresolvable dependency, a bad return-type union) fails here at test time instead of on
// first request in production. This is the Critter Stack replacement for the retired
// AssertWolverineConfigurationIsValid().
//
// The command stops the host it runs against, so this uses a throwaway host (reusing the shared
// fixture's Postgres in its own Marten schema) rather than the shared collection host.
[Collection(IntegrationCollection.Name)]
public class WolverineConfigurationShould(AppFixture fixture)
{
  [Fact]
  public async Task CompileEveryHandlerAndEndpoint()
  {
    var host = await FarkleTestHost.StartAsync(fixture.ConnectionString, "farkle_it_codegen");
    try
    {
      var exitCode = await host.RunJasperFxCommands(["codegen", "test"]);
      Assert.Equal(0, exitCode);
    }
    finally
    {
      await FarkleTestHost.StopTolerantlyAsync(host);
    }
  }
}
