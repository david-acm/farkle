using FastEndpoints;
using Farkle.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace WebApp.Auth;

internal class RegisterEndpoint(UserManager<AppUser> userManager, ILogger<RegisterEndpoint> logger)
    : Endpoint<RegisterRequest>
{
    public override void Configure()
    {
        Post("/api/auth/register");
        AllowAnonymous();
    }

    public override async Task HandleAsync(RegisterRequest req, CancellationToken ct)
    {
        var user = new AppUser { UserName = req.Email, Email = req.Email };
        var result = await userManager.CreateAsync(user, req.Password);

        if (!result.Succeeded)
        {
            // Structured telemetry (#33): log the email + the identity error codes, never the password.
            logger.LogWarning("Registration failed for {Email}: {Errors}",
                req.Email, result.Errors.Select(e => e.Code).ToArray());

            foreach (var error in result.Errors)
                AddError(error.Description);

            await Send.ErrorsAsync(400, ct);
            return;
        }

        logger.LogInformation("Registration succeeded for {Email}", req.Email);
        await Send.NoContentAsync(ct);
    }
}
