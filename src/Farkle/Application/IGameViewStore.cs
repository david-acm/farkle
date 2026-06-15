namespace Farkle.Application;

// #156 — the read side's persistence seam, kept EF-agnostic so the Farkle (domain) assembly
// stays infrastructure-free. The store deals in opaque snapshot JSON keyed by game id; the
// EF/Postgres implementation lives in WebApp. `position` is the $all global position last
// folded, persisted for observability / idempotency.
public interface IGameViewStore
{
  Task<string?> GetAsync(int gameId, CancellationToken ct);

  Task UpsertAsync(int gameId, string stateJson, ulong position, CancellationToken ct);
}

// Default no-op store for hosts without the read model (e.g. the in-memory storyboard
// capture, which has no Postgres). Get returns null so GetGameStateEndpoint falls back to
// replaying the aggregate; Upsert is a no-op. Registered via TryAddSingleton so the real
// EfGameViewStore (WebApp) takes precedence whenever it is registered.
internal sealed class NullGameViewStore : IGameViewStore
{
  public Task<string?> GetAsync(int gameId, CancellationToken ct) => Task.FromResult<string?>(null);

  public Task UpsertAsync(int gameId, string stateJson, ulong position, CancellationToken ct) =>
    Task.CompletedTask;
}
