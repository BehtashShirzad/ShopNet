using IdentityService.Contracts;
using Keycloak.Client.Models;

namespace IdentityService.Services;

public interface IIdentityProvider
{
    Task<string> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default);
    Task<KeycloakTokenResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
    Task<KeycloakTokenResponse> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default);
    Task LogoutAsync(string refreshToken, CancellationToken cancellationToken = default);
    Task<UserProfile?> GetUserAsync(string userId, CancellationToken cancellationToken = default);
    Task ChangePasswordAsync(string userId, string currentPassword, string newPassword,
        CancellationToken cancellationToken = default);
}
