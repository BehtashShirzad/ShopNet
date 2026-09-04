using IdentityService.Contracts;
using IdentityService.Services;
using Keycloak.Client;
using Keycloak.Client.Models;
using Moq;

namespace IdentityService.UnitTests.Services;

public sealed class KeycloakIdentityProviderTests
{
    [Fact]
    public async Task Register_MapsRequestToKeycloakClient()
    {
        var client = new Mock<IKeycloakClient>();
        client.Setup(x => x.RegisterUserAsync(
                It.IsAny<RegisterKeycloakUserRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("keycloak-user-id");
        var provider = new KeycloakIdentityProvider(client.Object);

        var id = await provider.RegisterAsync(
            new RegisterRequest("user@example.com", "Password1", "First", "Last"));

        Assert.Equal("keycloak-user-id", id);
        client.Verify(x => x.RegisterUserAsync(
            It.Is<RegisterKeycloakUserRequest>(request =>
                request.Email == "user@example.com" &&
                request.Password == "Password1" &&
                request.FirstName == "First" &&
                request.LastName == "Last"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task LoginAndRefresh_ReturnKeycloakTokens()
    {
        var loginToken = Token("access-1", "refresh-1");
        var refreshedToken = Token("access-2", "refresh-2");
        var client = new Mock<IKeycloakClient>();
        client.Setup(x => x.LoginAsync("user@example.com", "Password1", default))
            .ReturnsAsync(loginToken);
        client.Setup(x => x.RefreshTokenAsync("refresh-1", default))
            .ReturnsAsync(refreshedToken);
        var provider = new KeycloakIdentityProvider(client.Object);

        var login = await provider.LoginAsync(new LoginRequest("user@example.com", "Password1"));
        var refresh = await provider.RefreshAsync("refresh-1");

        Assert.Same(loginToken, login);
        Assert.Same(refreshedToken, refresh);
    }

    [Fact]
    public async Task ChangePassword_VerifiesCurrentPasswordBeforeReset()
    {
        var client = new Mock<IKeycloakClient>(MockBehavior.Strict);
        client.Setup(x => x.GetUserAsync("user-id", default)).ReturnsAsync(new KeycloakUser
        {
            Id = "user-id",
            Email = "user@example.com",
            Enabled = true
        });
        client.Setup(x => x.LoginAsync("user@example.com", "old-password", default))
            .ReturnsAsync(Token("access", "refresh"));
        client.Setup(x => x.ResetPasswordAsync("user-id", "new-password", false, default))
            .Returns(Task.CompletedTask);
        var provider = new KeycloakIdentityProvider(client.Object);

        await provider.ChangePasswordAsync("user-id", "old-password", "new-password");

        client.VerifyAll();
    }

    [Fact]
    public async Task GetUser_MapsKeycloakUserToPublicProfile()
    {
        var client = new Mock<IKeycloakClient>();
        client.Setup(x => x.GetUserAsync("user-id", default)).ReturnsAsync(new KeycloakUser
        {
            Id = "user-id",
            Username = "user@example.com",
            Email = "user@example.com",
            FirstName = "First",
            LastName = "Last",
            Enabled = true,
            EmailVerified = true
        });

        var profile = await new KeycloakIdentityProvider(client.Object).GetUserAsync("user-id");

        Assert.NotNull(profile);
        Assert.Equal("user@example.com", profile.Email);
        Assert.True(profile.EmailVerified);
    }

    private static KeycloakTokenResponse Token(string accessToken, string refreshToken) => new()
    {
        AccessToken = accessToken,
        RefreshToken = refreshToken,
        ExpiresIn = 300
    };
}
