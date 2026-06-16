using FastEndpoints;
using Farkle.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;

namespace WebApp.Auth;

internal class RegisterEndpoint(UserManager<AppUser> userManager)
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
            foreach (var error in result.Errors)
                AddError(error.Description);

            await Send.ErrorsAsync(400, ct);
            return;
        }

        await Send.NoContentAsync(ct);
    }
}
