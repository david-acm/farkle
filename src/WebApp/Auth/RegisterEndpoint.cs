using Farkle.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Wolverine.Http;

namespace WebApp.Auth;

// #303 — registration on Wolverine.HTTP (migrated off FastEndpoints). Anonymous. Creates an ASP.NET
// Identity user; identity errors become a 400 ValidationProblem. Never logs the password.
public static class RegisterEndpoint
{
  [AllowAnonymous]
  [WolverinePost("/api/auth/register")]
  public static async Task<Results<NoContent, ValidationProblem>> Post(
    RegisterRequest body, UserManager<AppUser> userManager, ILoggerFactory loggerFactory, CancellationToken ct)
  {
    var logger = loggerFactory.CreateLogger("WebApp.Auth.Register");

    var user   = new AppUser { UserName = body.Email, Email = body.Email };
    var result = await userManager.CreateAsync(user, body.Password);

    if (!result.Succeeded)
    {
      // Structured telemetry (#33): log the email + identity error codes, never the password.
      logger.LogWarning("Registration failed for {Email}: {Errors}",
        body.Email, result.Errors.Select(e => e.Code).ToArray());

      return TypedResults.ValidationProblem(new Dictionary<string, string[]>
      {
        ["Registration"] = result.Errors.Select(e => e.Description).ToArray()
      });
    }

    logger.LogInformation("Registration succeeded for {Email}", body.Email);
    return TypedResults.NoContent();
  }
}
