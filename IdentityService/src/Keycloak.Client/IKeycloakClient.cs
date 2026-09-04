using Keycloak.Client.Models;

namespace Keycloak.Client;

public interface IKeycloakClient
{
    Task<string> RegisterUserAsync(RegisterKeycloakUserRequest request, CancellationToken cancellationToken = default);
    Task<KeycloakTokenResponse> LoginAsync(string username, string password, CancellationToken cancellationToken = default);
    Task<KeycloakTokenResponse> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default);
    Task LogoutAsync(string refreshToken, CancellationToken cancellationToken = default);
    Task LogoutUserSessionsAsync(string userId, CancellationToken cancellationToken = default);
    Task<KeycloakUser?> GetUserAsync(string userId, CancellationToken cancellationToken = default);
    Task ResetPasswordAsync(string userId, string newPassword, bool temporary = false,
        CancellationToken cancellationToken = default);
    Task DeleteUserAsync(string userId, CancellationToken cancellationToken = default);
}
