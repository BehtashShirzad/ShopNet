using Keycloak.Client.Models;

namespace IdentityService.Contracts;

public sealed record RegisterRequest(string Email, string Password, string? FirstName, string? LastName);
public sealed record LoginRequest(string Email, string Password);
public sealed record RefreshTokenRequest(string RefreshToken);
public sealed record LogoutRequest(string RefreshToken);
public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public sealed record UserProfile(
    string Id,
    string? Username,
    string? Email,
    string? FirstName,
    string? LastName,
    bool Enabled,
    bool EmailVerified)
{
    public static UserProfile From(KeycloakUser user) => new(
        user.Id, user.Username, user.Email, user.FirstName, user.LastName,
        user.Enabled, user.EmailVerified);
}
