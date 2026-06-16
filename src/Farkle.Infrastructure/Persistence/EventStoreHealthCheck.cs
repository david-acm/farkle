using EventStore.Client;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Farkle.Infrastructure.Persistence;

// Readiness check for EventStore.
//
// Resolves EventStoreClient LAZILY from the service provider rather than taking it
// as a constructor dependency: the storyboard/E2E host (StoryboardWebAppFactory)
// removes EventStoreClient from DI and swaps in an in-memory store, so a constructor
// dependency would break that host's container validation. When the client is absent
// we report Unhealthy instead of throwing.
public sealed class EventStoreHealthCheck(IServiceProvider services) : IHealthCheck
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(2);

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var client = services.GetService(typeof(EventStoreClient)) as EventStoreClient;
        if (client is null)
            return HealthCheckResult.Unhealthy("EventStoreClient is not registered.");

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(Timeout);

        try
        {
            // Bounded reachability probe: read at most one event from $all. An empty
            // store completes without items (still reachable → healthy); an
            // unreachable/timed-out node throws.
            var result = client.ReadAllAsync(
                Direction.Forwards, Position.Start, maxCount: 1, cancellationToken: cts.Token);
            await foreach (var _ in result.WithCancellation(cts.Token))
                break;

            return HealthCheckResult.Healthy("EventStore is reachable.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException
                                   || !cancellationToken.IsCancellationRequested)
        {
            // Includes RpcException, connection failures, and our own timeout
            // (OperationCanceledException from the linked CancelAfter). A genuine
            // host-shutdown cancellation is rethrown by the filter above.
            return HealthCheckResult.Unhealthy("EventStore is not reachable.", ex);
        }
    }
}
