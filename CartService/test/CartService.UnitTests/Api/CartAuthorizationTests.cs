using CartService.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using ShopNet.Authorization;

namespace CartService.UnitTests.Api;

public sealed class CartAuthorizationTests
{
    [Fact]
    public void CartEndpoints_ExposeExpectedPermissionPolicies()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddHttpContextAccessor();
        var app = builder.Build();
        app.MapCartEndpoints();
        var policies = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(source => source.Endpoints)
            .Select(endpoint => endpoint.Metadata.GetMetadata<IAuthorizeData>()?.Policy)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(new[]
        {
            CartPermissions.Checkout,
            CartPermissions.Read,
            CartPermissions.Write,
            CartPermissions.Write
        }.Order(StringComparer.Ordinal), policies);
    }
}
