namespace WebApp.Auth;

public record RegisterRequest(string Email, string Password);

public record LoginRequest(string Email, string Password);

public record LoginResponse(string Token, DateTime ExpiresAt);
