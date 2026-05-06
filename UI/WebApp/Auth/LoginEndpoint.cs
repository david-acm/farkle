using System.Security.Claims;
using FastEndpoints;
using FastEndpoints.Security;
using Microsoft.AspNetCore.Identity;

namespace WebApp.Auth;

internal class LoginEndpoint(
    UserManager<AppUser> userManager,
    SignInManager<AppUser> signInManager,
    IConfiguration configuration)
    : Endpoint<LoginRequest, LoginResponse>
{
    public override void Configure()
    {
        Post("/api/auth/login");
        AllowAnonymous();
    }

    public override async Task HandleAsync(LoginRequest req, CancellationToken ct)
    {
        var user = await userManager.FindByEmailAsync(req.Email);
        if (user is null)
        {
            await SendUnauthorizedAsync(ct);
            return;
        }

        var result = await signInManager.CheckPasswordSignInAsync(user, req.Password, lockoutOnFailure: false);
        if (!result.Succeeded)
        {
            await SendUnauthorizedAsync(ct);
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

        await SendAsync(new LoginResponse(token, expiresAt), cancellation: ct);
    }
}
