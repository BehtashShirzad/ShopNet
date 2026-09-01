using System.Security.Claims;
using IdentityService.Models;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace IdentityService.Controllers;

[ApiController]
public class AuthorizationController : ControllerBase
{
    private readonly IOpenIddictApplicationManager _applicationManager;
    private readonly SignInManager<AppUser>         _signInManager;
    private readonly UserManager<AppUser>           _userManager;

    public AuthorizationController(
        IOpenIddictApplicationManager applicationManager,
        SignInManager<AppUser>         signInManager,
        UserManager<AppUser>           userManager)
    {
        _applicationManager = applicationManager;
        _signInManager       = signInManager;
        _userManager         = userManager;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // POST /connect/token
    // هم Client Credentials هم Authorization Code + Refresh Token رو اینجا هندل می‌کنیم
    // ──────────────────────────────────────────────────────────────────────────
    [HttpPost("~/connect/token")]
    [Consumes("application/x-www-form-urlencoded")]
    [Produces("application/json")]
    public async Task<IActionResult> Exchange()
    {
        var request = HttpContext.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("OpenIddict request not found.");

        // ── Client Credentials Flow ──────────────────────────────────────────
        if (request.IsClientCredentialsGrantType())
        {
            var application = await _applicationManager.FindByClientIdAsync(request.ClientId!);
            if (application is null)
                return Forbid(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);

            var identity = new ClaimsIdentity(
                OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                Claims.Name, Claims.Role);

            identity.AddClaim(Claims.Subject,
                await _applicationManager.GetClientIdAsync(application) ?? string.Empty);
            identity.AddClaim(Claims.Name,
                await _applicationManager.GetDisplayNameAsync(application) ?? string.Empty);

            // Scopeهایی که client درخواست داده (در صورت مجاز بودن)
            var principal = new ClaimsPrincipal(identity);
            principal.SetScopes(request.GetScopes());

            return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        // ── Authorization Code / Refresh Token Flow ──────────────────────────
        if (request.IsAuthorizationCodeGrantType() || request.IsRefreshTokenGrantType())
        {
            var result = await HttpContext.AuthenticateAsync(
                OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);

            var userId = result.Principal?.GetClaim(Claims.Subject);
            var user   = userId is not null ? await _userManager.FindByIdAsync(userId) : null;

            if (user is null)
            {
                return Forbid(
                    authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                    properties: new AuthenticationProperties(new Dictionary<string, string?>
                    {
                        [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.InvalidGrant,
                        [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = "User not found."
                    }));
            }

            var identity = new ClaimsIdentity(
                result.Principal!.Claims,
                OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                Claims.Name, Claims.Role);

            // اضافه کردن claim های کاربر
            identity.SetClaim(Claims.Subject,  user.Id);
            identity.SetClaim(Claims.Email,    user.Email);
            identity.SetClaim(Claims.Name,     user.UserName);
            identity.SetClaim("firstName",     user.FirstName ?? "");
            identity.SetClaim("lastName",      user.LastName  ?? "");

            // اضافه کردن Role ها
            var roles = await _userManager.GetRolesAsync(user);
            foreach (var role in roles)
                identity.AddClaim(Claims.Role, role);

            var principal = new ClaimsPrincipal(identity);
            principal.SetScopes(result.Principal!.GetScopes());

            // مشخص کردن کدام claim ها در access_token و id_token باشن
            identity.SetDestinations(claim => claim.Type switch
            {
                Claims.Name  when identity.HasScope(Scopes.Profile) => [Destinations.AccessToken, Destinations.IdentityToken],
                Claims.Email when identity.HasScope(Scopes.Email)   => [Destinations.AccessToken, Destinations.IdentityToken],
                Claims.Role                                          => [Destinations.AccessToken, Destinations.IdentityToken],
                "firstName" or "lastName"                            => [Destinations.AccessToken, Destinations.IdentityToken],
                _                                                    => [Destinations.AccessToken]
            });

            return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        throw new InvalidOperationException("Unsupported grant type.");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // GET /connect/authorize  — نقطه شروع Authorization Code Flow
    // ──────────────────────────────────────────────────────────────────────────
    [HttpGet("~/connect/authorize")]
    [HttpPost("~/connect/authorize")]
    public async Task<IActionResult> Authorize()
    {
        var request = HttpContext.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("OpenIddict request not found.");

        // بررسی آیا کاربر login کرده
        var result = await HttpContext.AuthenticateAsync(IdentityConstants.ApplicationScheme);

        if (!result.Succeeded)
        {
            // ریدایرکت به صفحه login
            return Challenge(
                authenticationSchemes: IdentityConstants.ApplicationScheme,
                properties: new AuthenticationProperties
                {
                    RedirectUri = Request.PathBase + Request.Path + QueryString.Create(
                        Request.HasFormContentType
                            ? Request.Form.ToList()
                            : Request.Query.ToList())
                });
        }

        var user = await _userManager.GetUserAsync(result.Principal!)
            ?? throw new InvalidOperationException("User not found.");

        var identity = new ClaimsIdentity(
            OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
            Claims.Name, Claims.Role);

        identity.SetClaim(Claims.Subject,  user.Id);
        identity.SetClaim(Claims.Email,    user.Email);
        identity.SetClaim(Claims.Name,     user.UserName);
        identity.SetClaim("firstName",     user.FirstName ?? "");
        identity.SetClaim("lastName",      user.LastName  ?? "");

        var roles = await _userManager.GetRolesAsync(user);
        foreach (var role in roles)
            identity.AddClaim(Claims.Role, role);

        var principal = new ClaimsPrincipal(identity);
        principal.SetScopes(request.GetScopes());

        identity.SetDestinations(claim => claim.Type switch
        {
            Claims.Email when identity.HasScope(Scopes.Email)    => [Destinations.AccessToken, Destinations.IdentityToken],
            Claims.Name  when identity.HasScope(Scopes.Profile)  => [Destinations.AccessToken, Destinations.IdentityToken],
            Claims.Role                                           => [Destinations.AccessToken, Destinations.IdentityToken],
            "firstName" or "lastName"                             => [Destinations.AccessToken, Destinations.IdentityToken],
            _                                                     => [Destinations.AccessToken]
        });

        return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // GET /connect/userinfo
    // ──────────────────────────────────────────────────────────────────────────
    [HttpGet("~/connect/userinfo")]
    [HttpPost("~/connect/userinfo")]
    public async Task<IActionResult> Userinfo()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null)
            return Challenge(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);

        var claims = new Dictionary<string, object>
        {
            [Claims.Subject]  = user.Id,
            [Claims.Email]    = user.Email!,
            [Claims.Name]     = user.UserName!,
            ["firstName"]     = user.FirstName ?? "",
            ["lastName"]      = user.LastName  ?? "",
            ["emailVerified"] = user.EmailConfirmed
        };

        return Ok(claims);
    }
}