using Microsoft.AspNetCore.Mvc.Testing;

namespace Farkle.WebTests;

/// <summary>
/// Base test factory that tolerates harmless teardown noise: shutting the host down can surface an
/// <see cref="ObjectDisposedException"/> (sometimes wrapped in an <see cref="AggregateException"/>)
/// from the Marten/Wolverine background durability agent as it races the host stop. It's harmless
/// (the host is tearing down), but it would otherwise surface as an xUnit class-cleanup failure and
/// fail the test run. Swallow just that exception on disposal.
/// </summary>
public abstract class FarkleWebApplicationFactory : WebApplicationFactory<Program>
{
  protected override void Dispose(bool disposing)
  {
    try { base.Dispose(disposing); }
    catch (ObjectDisposedException) { }
    catch (AggregateException ex) when (ex.Flatten().InnerExceptions.All(e => e is ObjectDisposedException)) { }
  }

  public override async ValueTask DisposeAsync()
  {
    try { await base.DisposeAsync(); }
    catch (ObjectDisposedException) { }
    catch (AggregateException ex) when (ex.Flatten().InnerExceptions.All(e => e is ObjectDisposedException)) { }
  }
}
