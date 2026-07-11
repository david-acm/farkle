using System.Security.Claims;
using System.Text;
using Farkle.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace WebApp.Auth;

// #303 — login on Wolverine.HTTP (migrated off FastEndpoints.Security). Anonymous. Verifies the
// credentials and mints a 4-hour HMAC-SHA256 JWT with the standard IdentityModel handler (replacing
// FastEndpoints' JwtBearer.CreateToken). Never logs the password or the issued token.
public static class LoginEndpoint
{
  [AllowAnonymous]
  [Wolverine.Http.WolverinePost("/api/auth/login")]
  public static async Task<Results<Ok<LoginResponse>, UnauthorizedHttpResult>> Post(
    LoginRequest body,
    UserManager<AppUser> userManager,
    SignInManager<AppUser> signInManager,
    IConfiguration configuration,
    ILoggerFactory loggerFactory,
    CancellationToken ct)
  {
    var logger = loggerFactory.CreateLogger("WebApp.Auth.Login");

    var user = await userManager.FindByEmailAsync(body.Email);
    if (user is null)
    {
      logger.LogWarning("Login failed for {Email}: {Reason}", body.Email, "user not found");
      return TypedResults.Unauthorized();
    }

    var result = await signInManager.CheckPasswordSignInAsync(user, body.Password, lockoutOnFailure: false);
    if (!result.Succeeded)
    {
      logger.LogWarning("Login failed for {Email}: {Reason}", body.Email, "invalid password");
      return TypedResults.Unauthorized();
    }

    var expiresAt = DateTime.UtcNow.AddHours(4);
    var token     = CreateToken(configuration["Auth:JwtSecret"]!, user, expiresAt);

    logger.LogInformation("Login succeeded for {Email}", body.Email);
    return TypedResults.Ok(new LoginResponse(token, expiresAt));
  }

  private static string CreateToken(string secret, AppUser user, DateTime expiresAt)
  {
    var descriptor = new SecurityTokenDescriptor
    {
      Subject = new ClaimsIdentity(
      [
        new Claim(ClaimTypes.NameIdentifier, user.Id),
        new Claim(ClaimTypes.Email, user.Email!)
      ]),
      Expires            = expiresAt,
      SigningCredentials = new SigningCredentials(
        new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)), SecurityAlgorithms.HmacSha256),
    };

    return new JsonWebTokenHandler().CreateToken(descriptor);
  }
}
