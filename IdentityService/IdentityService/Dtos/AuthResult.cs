namespace IdentityService.Dtos;

public record AuthResult(bool Success, string? UserId, string? Error)
{
    public static AuthResult Ok(string userId)    => new(true,  userId, null);
    public static AuthResult Fail(string error)   => new(false, null,   error);
}