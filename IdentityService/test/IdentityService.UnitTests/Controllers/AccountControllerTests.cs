using System.Security.Claims;
using IdentityService.Controllers;
using IdentityService.Dtos;
using IdentityService.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace IdentityService.UnitTests;

public class AccountControllerTests
{
    [Fact]
    public async Task Register_ReturnsBadRequestWhenServiceFails()
    {
        var service = new Mock<IAuthService>();
        service.Setup(x => x.RegisterAsync(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string?>(), It.IsAny<string?>()))
            .ReturnsAsync(AuthResult.Fail("duplicate"));

        var result = await new AccountController(service.Object).Register(
            new RegisterRequest("user@example.com", "Password1", "First", "Last"));

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Register_ReturnsUserIdWhenServiceSucceeds()
    {
        var service = new Mock<IAuthService>();
        service.Setup(x => x.RegisterAsync(
                "user@example.com", "Password1", "First", "Last"))
            .ReturnsAsync(AuthResult.Ok("user-id"));

        var result = await new AccountController(service.Object).Register(
            new RegisterRequest("user@example.com", "Password1", "First", "Last"));

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal("user-id", ReadProperty<string>(ok.Value, "userId"));
    }

    [Fact]
    public async Task Login_MapsFailureAndSuccessToHttpResults()
    {
        var service = new Mock<IAuthService>();
        service.SetupSequence(x => x.LoginAsync("user@example.com", "Password1"))
            .ReturnsAsync(AuthResult.Fail("invalid"))
            .ReturnsAsync(AuthResult.Ok("user-id"));
        var controller = new AccountController(service.Object);

        var failed = await controller.Login(
            new LoginRequest("user@example.com", "Password1"));
        var succeeded = await controller.Login(
            new LoginRequest("user@example.com", "Password1"));

        Assert.IsType<BadRequestObjectResult>(failed);
        Assert.IsType<OkObjectResult>(succeeded);
    }

    [Fact]
    public async Task GetProfile_RequiresSubjectClaim()
    {
        var controller = CreateController(Mock.Of<IAuthService>());

        var result = await controller.GetProfile();

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task GetProfile_ReturnsNotFoundOrUser()
    {
        var service = new Mock<IAuthService>();
        service.SetupSequence(x => x.GetUserByIdAsync("user-id"))
            .ReturnsAsync((UserDto?)null)
            .ReturnsAsync(new UserDto(
                "user-id", "user@example.com", "First", "Last", ["User"]));
        var controller = CreateController(service.Object, "user-id");

        var missing = await controller.GetProfile();
        var found = await controller.GetProfile();

        Assert.IsType<NotFoundResult>(missing);
        var ok = Assert.IsType<OkObjectResult>(found);
        Assert.Equal("user-id", Assert.IsType<UserDto>(ok.Value).Id);
    }

    [Fact]
    public async Task ChangePassword_UsesAuthenticatedUserId()
    {
        var service = new Mock<IAuthService>();
        service.Setup(x => x.ChangePasswordAsync("user-id", "old", "new"))
            .ReturnsAsync(AuthResult.Ok("user-id"));
        var controller = CreateController(service.Object, "user-id");

        var result = await controller.ChangePassword(
            new ChangePasswordRequest("old", "new"));

        Assert.IsType<OkObjectResult>(result);
        service.Verify(x => x.ChangePasswordAsync("user-id", "old", "new"), Times.Once);
    }

    [Fact]
    public async Task Logout_UsesSubjectAndReturnsOk()
    {
        var service = new Mock<IAuthService>();
        service.Setup(x => x.LogoutAsync("user-id")).ReturnsAsync(true);
        var controller = CreateController(service.Object, "user-id");

        var result = await controller.Logout();

        Assert.IsType<OkObjectResult>(result);
        service.Verify(x => x.LogoutAsync("user-id"), Times.Once);
    }

    private static AccountController CreateController(
        IAuthService service, string? userId = null)
    {
        var claims = userId is null
            ? Array.Empty<Claim>()
            : [new Claim("sub", userId)];
        return new AccountController(service)
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

    private static T? ReadProperty<T>(object? instance, string name) =>
        (T?)instance?.GetType().GetProperty(name)?.GetValue(instance);
}
