using IdentityService.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using OpenIddict.Abstractions;

namespace IdentityService.UnitTests;

public class AuthorizationControllerTests
{
    [Fact]
    public async Task Exchange_WithoutOpenIddictRequest_ThrowsClearError()
    {
        var controller = CreateController();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => controller.Exchange());

        Assert.Equal("OpenIddict request not found.", exception.Message);
    }

    [Fact]
    public async Task Authorize_WithoutOpenIddictRequest_ThrowsClearError()
    {
        var controller = CreateController();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => controller.Authorize());

        Assert.Equal("OpenIddict request not found.", exception.Message);
    }

    private static AuthorizationController CreateController() => new(
        Mock.Of<IOpenIddictApplicationManager>(), null!, null!)
    {
        ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        }
    };
}
