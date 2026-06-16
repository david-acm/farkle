using FastEndpoints;

namespace Farkle.Endpoints;

// Base for the game's FastEndpoints. Lives in the Farkle project (its only consumer) rather
// than in a "shared kernel" — it's web plumbing, not a shared domain type.
public class TypedEndpoint<TRequest, TResponse> : Endpoint<TRequest, TResponse>
  where TRequest : notnull
{
}
