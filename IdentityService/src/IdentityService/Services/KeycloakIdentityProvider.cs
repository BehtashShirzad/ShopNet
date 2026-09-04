using IdentityService.Contracts;
using Keycloak.Client;
using Keycloak.Client.Models;

namespace IdentityService.Services;

public sealed class KeycloakIdentityProvider(IKeycloakClient client) : IIdentityProvider
{
    public Task<string> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default) =>
        client.RegisterUserAsync(new RegisterKeycloakUserRequest(
            request.Email, request.Password, request.FirstName, request.LastName), cancellationToken);

    public Task<KeycloakTokenResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default) =>
        client.LoginAsync(request.Email, request.Password, cancellationToken);

    public Task<KeycloakTokenResponse> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default) =>
        client.RefreshTokenAsync(refreshToken, cancellationToken);

    public Task LogoutAsync(string refreshToken, CancellationToken cancellationToken = default) =>
        client.LogoutAsync(refreshToken, cancellationToken);

    public async Task<UserProfile?> GetUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        var user = await client.GetUserAsync(userId, cancellationToken);
        return user is null ? null : UserProfile.From(user);
    }

    public async Task ChangePasswordAsync(
        string userId,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        var user = await client.GetUserAsync(userId, cancellationToken)
            ?? throw new InvalidOperationException("The authenticated user no longer exists.");
        var username = user.Email ?? user.Username
            ?? throw new InvalidOperationException("The Keycloak user has no login name.");

        await client.LoginAsync(username, currentPassword, cancellationToken);
        await client.ResetPasswordAsync(userId, newPassword, cancellationToken: cancellationToken);
    }
}
