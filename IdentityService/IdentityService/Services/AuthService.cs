using IdentityService.Dtos;
using IdentityService.Models;
using Microsoft.AspNetCore.Identity;

namespace IdentityService.Services;

 
public class AuthService : IAuthService
{
    private readonly UserManager<AppUser>   _userManager;
    private readonly SignInManager<AppUser> _signInManager;
    private readonly ILogger<AuthService>   _logger;
 
    public AuthService(
        UserManager<AppUser>   userManager,
        SignInManager<AppUser> signInManager,
        ILogger<AuthService>   logger)
    {
        _userManager   = userManager;
        _signInManager = signInManager;
        _logger        = logger;
    }
 
    // ── Register ─────────────────────────────────────────────────────────────
    public async Task<AuthResult> RegisterAsync(
        string email, string password, string? firstName, string? lastName)
    {
        var existingUser = await _userManager.FindByEmailAsync(email);
        if (existingUser is not null)
            return AuthResult.Fail("این ایمیل قبلاً ثبت شده است.");
 
        var user = new AppUser
        {
            UserName  = email,
            Email     = email,
            FirstName = firstName,
            LastName  = lastName
        };
 
        var result = await _userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            _logger.LogWarning("Register failed for {Email}: {Errors}", email, errors);
            return AuthResult.Fail(errors);
        }
 
        await _userManager.AddToRoleAsync(user, "User");
        _logger.LogInformation("New user registered: {Email}", email);
 
        return AuthResult.Ok(user.Id);
    }
 
    // ── Login ─────────────────────────────────────────────────────────────────
    public async Task<AuthResult> LoginAsync(string email, string password)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user is null)
            return AuthResult.Fail("ایمیل یا رمز عبور اشتباه است.");
 
        var result = await _signInManager.PasswordSignInAsync(
            user, password, isPersistent: false, lockoutOnFailure: true);
 
        if (result.Succeeded)
        {
            _logger.LogInformation("User logged in: {Email}", email);
            return AuthResult.Ok(user.Id);
        }
 
        if (result.IsLockedOut)
            return AuthResult.Fail("حساب کاربری قفل شده. بعداً تلاش کنید.");
 
        if (result.RequiresTwoFactor)
            return AuthResult.Fail("احراز هویت دو مرحله‌ای نیاز است.");
 
        return AuthResult.Fail("ایمیل یا رمز عبور اشتباه است.");
    }
 
    // ── Logout ────────────────────────────────────────────────────────────────
    public async Task<bool> LogoutAsync(string userId)
    {
        await _signInManager.SignOutAsync();
        _logger.LogInformation("User logged out: {UserId}", userId);
        return true;
    }
 
    // ── Get User ──────────────────────────────────────────────────────────────
    public async Task<UserDto?> GetUserByIdAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null) return null;
 
        var roles = await _userManager.GetRolesAsync(user);
 
        return new UserDto(
            user.Id,
            user.Email!,
            user.FirstName,
            user.LastName,
            roles.ToList()
        );
    }
 
    // ── Change Password ───────────────────────────────────────────────────────
    public async Task<AuthResult> ChangePasswordAsync(
        string userId, string currentPassword, string newPassword)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
            return AuthResult.Fail("کاربر یافت نشد.");
 
        var result = await _userManager.ChangePasswordAsync(user, currentPassword, newPassword);
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            return AuthResult.Fail(errors);
        }
 
        _logger.LogInformation("Password changed for user: {UserId}", userId);
        return AuthResult.Ok(userId);
    }
}
 
// ── DTOs ──────────────────────────────────────────────────────────────────────

 
