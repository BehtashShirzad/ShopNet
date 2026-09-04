using CatalogService.Api.Endpoints;
using CatalogService.Api.Routes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using ShopNet.Authorization;

namespace CatalogService.UnitTests.Api;

public sealed class CatalogAuthorizationTests
{
    [Fact]
    public void CatalogEndpoints_ExposeExpectedPermissionPoliciesAndOnePublicRead()
    {
        var app = WebApplication.CreateBuilder().Build();
        app.MapProductEndpoints();
        app.MapCategoryEndpoints();
        var endpoints = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(source => source.Endpoints).ToArray();
        var policies = endpoints
            .Select(endpoint => endpoint.Metadata.GetMetadata<IAuthorizeData>()?.Policy)
            .Where(policy => policy is not null)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(new[]
        {
            CatalogPermissions.CategoryCreate,
            CatalogPermissions.CategoryUpdate,
            CatalogPermissions.ProductCreate,
            CatalogPermissions.ProductUpdate
        }.Order(StringComparer.Ordinal), policies);
        Assert.Single(endpoints.Where(endpoint =>
            endpoint.Metadata.GetMetadata<IAuthorizeData>() is null));
    }
}
