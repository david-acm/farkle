using Eventuous;
using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Eventuous.AspNetCore.Web;
using ProblemDetails=Microsoft.AspNetCore.Mvc.ProblemDetails;

namespace Farkle.WebApi;


// TODO: Clean and refactor
internal static class ResultExtensions
{
  public static IResult AsMinimalResult<TState>(this Result<TState> resultOf)
    where TState : State<TState>, new()
    => resultOf switch
    {
      ErrorResult<TState> errorResult => errorResult.ToMinimalApiResult(),
      OkResult<TState> result         => new Result(result.State,   result.Success,   result.Changes).AsResult(),
      _                               => new Result(resultOf.State, resultOf.Success, resultOf.Changes).AsResult()
    };
  
  public static async Task SendResponseAsync<TState, TResponse>(
    this IEndpoint ep, Result<TState> result,
    Func<Result<TState>, TResponse> mapper)
    where TState : State<TState>, new()
  {
    var response = ep.HttpContext.Response;
    if (result is not ErrorResult<TState> errorResult)
    {
      await response.SendOkAsync(mapper(result));
      return;
    }

    switch (errorResult.Exception)
    {
      case OptimisticConcurrencyException:
        await response.SendAsync(mapper(result));
        break;

      case AggregateNotFoundException:
        await response.SendNotFoundAsync();
        break;

      case DomainException e:
        await response.SendAsync(new ProblemDetails()
        {
          Status = 409, Detail = e.Message
        }, 409);
        break;
    }
  }

  internal static IResult ToMinimalApiResult<TState>(this Result<TState> result)
    where TState : State<TState>, new()
  {
    return result switch
    {
      ErrorResult<TState> errorResult => AsErrorResult(errorResult),
      OkResult<TState>                => Results.Ok(result),
      _                               => Results.StatusCode(500)
    };

    IResult AsErrorResult(ErrorResult<TState> errorResult)
    {
      return errorResult.Exception switch
      {
        OptimisticConcurrencyException => ConflictEntity(),
        AggregateNotFoundException     => NotFoundEntity(),
        DomainException                => UnprocessableEntity(),
        _                              => AsProblem(500)
      };

      IResult ConflictEntity() =>
        CreateResult(new ProblemDetails(), 409);
      
      IResult NotFoundEntity() =>
        CreateResult(new ProblemDetails(), 404);

      IResult UnprocessableEntity() =>
        CreateResult(new ValidationProblemDetails(AsErrors(errorResult)), 400);

      IResult AsProblem(int statusCode) => CreateResult(new ProblemDetails(), statusCode);
      
      IResult CreateResult<T>(T details, int statusCode) where T : ProblemDetails
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
        {"Domain", [errorResult.ErrorMessage ?? string.Empty]
        }
      };
    }
  }
}
