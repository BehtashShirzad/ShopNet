using IdentityService.Dtos;

namespace IdentityService.Services;

public interface IAuthService
{
    Task<AuthResult> RegisterAsync(string email, string password, string? firstName, string? lastName);
    Task<AuthResult> LoginAsync(string email, string password);
    Task<bool>       LogoutAsync(string userId);
    Task<UserDto?>   GetUserByIdAsync(string userId);
    Task<AuthResult> ChangePasswordAsync(string userId, string currentPassword, string newPassword);
}