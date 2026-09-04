using System.Security.Claims;
using IdentityService.Contracts;
using IdentityService.Controllers;
using IdentityService.Services;
using Keycloak.Client.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace IdentityService.UnitTests.Controllers;

public sealed class AccountControllerTests
{
    [Fact]
    public async Task Login_ReturnsTokenResponse()
    {
        var token = new KeycloakTokenResponse { AccessToken = "access-token", ExpiresIn = 300 };
        var provider = new Mock<IIdentityProvider>();
        provider.Setup(x => x.LoginAsync(It.IsAny<LoginRequest>(), default)).ReturnsAsync(token);

        var result = await Create(provider.Object).Login(
            new LoginRequest("user@example.com", "Password1"), default);

        Assert.Same(token, Assert.IsType<OkObjectResult>(result).Value);
    }

    [Fact]
    public async Task Profile_UsesJwtSubject()
    {
        var provider = new Mock<IIdentityProvider>();
        provider.Setup(x => x.GetUserAsync("user-id", default)).ReturnsAsync(new UserProfile(
            "user-id", "user", "user@example.com", null, null, true, false));

        var result = await Create(provider.Object, "user-id").GetProfile(default);

        var profile = Assert.IsType<UserProfile>(Assert.IsType<OkObjectResult>(result).Value);
        Assert.Equal("user-id", profile.Id);
    }

    [Fact]
    public async Task ProfileWithoutSubject_ReturnsUnauthorized()
    {
        var result = await Create(Mock.Of<IIdentityProvider>()).GetProfile(default);

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task Logout_ForwardsRefreshToken()
    {
        var provider = new Mock<IIdentityProvider>();
        var result = await Create(provider.Object, "user-id")
            .Logout(new LogoutRequest("refresh-token"), default);

        Assert.IsType<NoContentResult>(result);
        provider.Verify(x => x.LogoutAsync("refresh-token", default), Times.Once);
    }

    private static AccountController Create(IIdentityProvider provider, string? subject = null)
    {
        var claims = subject is null ? [] : new[] { new Claim("sub", subject) };
        return new AccountController(provider)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"))
                }
            }
        };
    }
}
