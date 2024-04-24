using Eventuous;
using FastEndpoints;
using Mapster;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ProblemDetails=Microsoft.AspNetCore.Mvc.ProblemDetails;

namespace Farkle.SharedKernel;

public static class ResultExtensions
{
  public static async Task SendResponseAsync<TState, TResponse>(
    this IEndpoint                            ep, Eventuous.Result<TState> result,
    Func<Eventuous.Result<TState>, TResponse> mapper)
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
