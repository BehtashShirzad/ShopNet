using System.IdentityModel.Tokens.Jwt;
using IdentityService.IntegrationTests.Fixtures;
using Keycloak.Client.Models;

namespace IdentityService.IntegrationTests.Api;

[Collection(KeycloakCollection.Name)]
public sealed class KeycloakIdentityIntegrationTests(KeycloakFixture fixture)
{
    [Fact]
    public async Task RegisterLoginRefreshAndLogout_WorkAgainstKeycloakContainer()
    {
        using var client = fixture.CreateClient();
        var email = $"user-{Guid.NewGuid():N}@example.com";

        var userId = await client.RegisterUserAsync(new RegisterKeycloakUserRequest(
            email, "Password1", "First", "Last"));
        var user = await client.GetUserAsync(userId);
        var login = await client.LoginAsync(email, "Password1");
        var refreshed = await client.RefreshTokenAsync(login.RefreshToken!);

        Assert.Equal(email, user?.Email);
        Assert.False(string.IsNullOrWhiteSpace(login.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(refreshed.AccessToken));
        Assert.Contains("shopnet-api", new JwtSecurityTokenHandler()
            .ReadJwtToken(login.AccessToken).Audiences);

        await client.LogoutAsync(refreshed.RefreshToken!);
    }
}
