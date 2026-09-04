using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using OrderService.Api;
using ShopNet.Authorization;

namespace OrderService.UnitTests.Api;

public sealed class OrderAuthorizationTests
{
    [Fact]
    public void GetOrder_RequiresReadOwnPermission()
    {
        var builder = WebApplication.CreateBuilder();
        var app = builder.Build();
        app.MapEndpoint();
        var endpoint = ((IEndpointRouteBuilder)app).DataSources.SelectMany(x => x.Endpoints)
            .OfType<RouteEndpoint>()
            .Single();

        Assert.Equal(OrderPermissions.ReadOwn,
            endpoint.Metadata.GetMetadata<IAuthorizeData>()?.Policy);
    }
}
