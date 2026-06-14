using Microsoft.AspNetCore.Mvc.Testing;

namespace Farkle.WebTests;

/// <summary>
/// Base test factory that tolerates a known Eventuous 0.15.0-beta shutdown bug: the broadcast
/// subscription's <c>CheckpointCommitHandler.DisposeAsync</c> double-disposes a
/// <c>CancellationTokenSource</c> and throws <see cref="ObjectDisposedException"/> from
/// <c>Host.StopAsync</c> during teardown. It's harmless (the host is tearing down), but it
/// would otherwise surface as an xUnit class-cleanup failure and fail the test run. Swallow
/// just that exception on disposal.
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
