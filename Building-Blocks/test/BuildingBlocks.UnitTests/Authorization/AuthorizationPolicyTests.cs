using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShopNet.Authorization;

namespace BuildingBlocks.UnitTests.Authorization;

public sealed class AuthorizationPolicyTests
{
    [Fact]
    public async Task PermissionPolicy_AcceptsOnlyMatchingPermissionClaim()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Keycloak:Authority"] = "http://keycloak/realms/shopnet",
            ["Keycloak:Audience"] = "shopnet-api",
            ["Keycloak:RequireHttpsMetadata"] = "false"
        }).Build();
        services.AddLogging();
        services.AddShopNetAuthorization(configuration, CatalogPermissions.ProductCreate);
        await using var provider = services.BuildServiceProvider();
        var authorization = provider.GetRequiredService<IAuthorizationService>();
        var allowed = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "user-id"),
            new Claim(ShopNetClaims.Permission, CatalogPermissions.ProductCreate)
        }, "test"));
        var denied = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "user-id"),
            new Claim(ShopNetClaims.Permission, CatalogPermissions.ProductUpdate)
        }, "test"));

        Assert.True((await authorization.AuthorizeAsync(
            allowed, null, CatalogPermissions.ProductCreate)).Succeeded);
        Assert.False((await authorization.AuthorizeAsync(
            denied, null, CatalogPermissions.ProductCreate)).Succeeded);
    }

    [Fact]
    public void PermissionNames_AreUniqueAcrossServices()
    {
        var permissions = CatalogPermissions.All
            .Concat(CartPermissions.All)
            .Concat(InventoryPermissions.All)
            .Concat(OrderPermissions.All)
            .ToArray();

        Assert.Equal(permissions.Length, permissions.Distinct(StringComparer.Ordinal).Count());
    }
}
