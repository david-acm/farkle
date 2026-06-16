using Eventuous;
using Mapster;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ProblemDetails=Microsoft.AspNetCore.Mvc.ProblemDetails;

namespace Farkle.Application;

// Maps an Eventuous Result<TState> to an HTTP result. This is web/application plumbing
// (FastEndpoints + Eventuous + ASP.NET), so it lives in Farkle next to its only consumer
// (GameService) rather than in the shared kernel, which is now infra-free.
public static class ResultExtensions
{
  public static IResult ToMinimalApiResult<TState, TResponse>(this Eventuous.Result<TState> result, Func<TState, TResponse>? mapper = null)
    where TState : State<TState>, new()
  {
    mapper ??= (TState state) => state.Adapt<TResponse>();

    return result switch
    {
      ErrorResult<TState> errorResult => ToMinimalApiErrorResult(errorResult),
      OkResult<TState> okResult       => Results.Ok(mapper(okResult.State!)),
      _                               => Results.StatusCode(500)
    };

    IResult ToMinimalApiErrorResult(ErrorResult<TState> errorResult)
    {
      return errorResult.Exception switch
      {
        OptimisticConcurrencyException => ConflictEntity(),
        AggregateNotFoundException     => NotFoundEntity(),
        DomainException                => UnprocessableEntity(),
        _                              => AsProblem(500)
      };

      IResult ConflictEntity() =>
        CreateProblemResult(new ProblemDetails(), 409);

      IResult NotFoundEntity() =>
        CreateProblemResult(new ProblemDetails(), 404);

      IResult UnprocessableEntity() =>
        CreateProblemResult(new ValidationProblemDetails(AsErrors(errorResult)), 400);

      IResult AsProblem(int statusCode) => CreateProblemResult(new ProblemDetails(), statusCode);

      IResult CreateProblemResult<T>(T details, int statusCode) where T : ProblemDetails
      {
        details.Status = statusCode;
        details.Title  = errorResult.ErrorMessage;
        details.Detail = errorResult.Exception?.Message;
        details.Type   = errorResult.Exception?.GetType().Name;

        return Results.Problem(details);
      }
    }

    Dictionary<string, string[]> AsErrors(ErrorResult<TState> errorResult)
    {
      return new Dictionary<string, string[]>()
      {
        {
          "Domain", [errorResult.ErrorMessage ?? string.Empty]
        }
      };
    }
  }
}
