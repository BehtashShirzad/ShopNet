using IdentityService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IdentityService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AccountController : ControllerBase
{
    private readonly IAuthService _authService;

    public AccountController(IAuthService authService)
    {
        _authService = authService;
    }

    // ── Register ─────────────────────────────────────────────────────────────
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest req)
    {
        var result = await _authService.RegisterAsync(
            req.Email, req.Password, req.FirstName, req.LastName);

        if (!result.Success)
            return BadRequest(new { error = result.Error });

        return Ok(new { message = "ثبت‌نام با موفقیت انجام شد.", userId = result.UserId });
    }

    // ── Login ─────────────────────────────────────────────────────────────────
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        var result = await _authService.LoginAsync(req.Email, req.Password);

        if (!result.Success)
            return BadRequest(new { error = result.Error });

        return Ok(new { message = "ورود با موفقیت انجام شد." });
    }

    // ── Logout ────────────────────────────────────────────────────────────────
    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        var userId = User.FindFirst("sub")?.Value ?? "";
        await _authService.LogoutAsync(userId);
        return Ok(new { message = "خروج با موفقیت انجام شد." });
    }

    // ── Get Profile ───────────────────────────────────────────────────────────
    [HttpGet("profile")]
    [Authorize]
    public async Task<IActionResult> GetProfile()
    {
        var userId = User.FindFirst("sub")?.Value;
        if (userId is null) return Unauthorized();

        var user = await _authService.GetUserByIdAsync(userId);
        if (user is null) return NotFound();

        return Ok(user);
    }

    // ── Change Password ───────────────────────────────────────────────────────
    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest req)
    {
        var userId = User.FindFirst("sub")?.Value;
        if (userId is null) return Unauthorized();

        var result = await _authService.ChangePasswordAsync(userId, req.CurrentPassword, req.NewPassword);

        if (!result.Success)
            return BadRequest(new { error = result.Error });

        return Ok(new { message = "رمز عبور با موفقیت تغییر کرد." });
    }
}

// ── DTOs ──────────────────────────────────────────────────────────────────────
public record RegisterRequest(string Email, string Password, string? FirstName, string? LastName);
public record LoginRequest(string Email, string Password);
public record ChangePasswordRequest(string CurrentPassword, string NewPassword);