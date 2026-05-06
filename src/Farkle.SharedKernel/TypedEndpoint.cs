using Ardalis.Result;
using FastEndpoints;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Farkle.Endpoints;

public class TypedEndpoint<TRequest, TResponse> : Endpoint<
  TRequest,
  Results<Ok<Result<TResponse>>, ProblemDetails>>
  where TRequest : notnull
{
  
}
