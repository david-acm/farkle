using FastEndpoints;

namespace Farkle.Endpoints;

public class TypedEndpoint<TRequest, TResponse> : Endpoint<TRequest, TResponse>
  where TRequest : notnull
{
}
