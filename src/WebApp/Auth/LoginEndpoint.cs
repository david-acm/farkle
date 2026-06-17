using System.Security.Claims;
using FastEndpoints;
using FastEndpoints.Security;
using Farkle.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace WebApp.Auth;

internal class LoginEndpoint(
    UserManager<AppUser> userManager,
    SignInManager<AppUser> signInManager,
    IConfiguration configuration,
    ILogger<LoginEndpoint> logger)
    : Endpoint<LoginRequest, LoginResponse>
{
    public override void Configure()
    {
        Post("/api/auth/login");
        AllowAnonymous();
    }

    public override async Task HandleAsync(LoginRequest req, CancellationToken ct)
    {
        // Structured telemetry (#33): log the outcome and the email only — never the password
        // or the issued token.
        var user = await userManager.FindByEmailAsync(req.Email);
        if (user is null)
        {
            logger.LogWarning("Login failed for {Email}: {Reason}", req.Email, "user not found");
            await Send.UnauthorizedAsync(ct);
            return;
        }

        var result = await signInManager.CheckPasswordSignInAsync(user, req.Password, lockoutOnFailure: false);
        if (!result.Succeeded)
        {
            logger.LogWarning("Login failed for {Email}: {Reason}", req.Email, "invalid password");
            await Send.UnauthorizedAsync(ct);
            return;
        }

        var expiresAt = DateTime.UtcNow.AddHours(4);

        var token = JwtBearer.CreateToken(
            o =>
            {
                o.SigningKey = configuration["Auth:JwtSecret"]!;
                o.ExpireAt = expiresAt;
                o.User.Claims.Add(new Claim(ClaimTypes.NameIdentifier, user.Id));
                o.User.Claims.Add(new Claim(ClaimTypes.Email, user.Email!));
            });

        logger.LogInformation("Login succeeded for {Email}", req.Email);
        await Send.OkAsync(new LoginResponse(token, expiresAt), ct);
    }
}
