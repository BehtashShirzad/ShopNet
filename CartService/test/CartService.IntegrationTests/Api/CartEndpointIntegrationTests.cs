using System.Net.Http.Json;
using CartService.Api;
using CartService.Application.Commands;
using CartService.Application.Query;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace CartService.IntegrationTests;

[Collection(CartContainersCollection.Name)]
public class CartEndpointIntegrationTests
{
    [Fact]
    public async Task GetCart_ReturnsSenderResponseOverHttp()
    {
        var cartId = Guid.NewGuid();
        var expected = new CartDto([
            new ProductViewModelOutput(Guid.NewGuid(), 2, 8m, "Product")
        ], 16m);
        var sender = new Mock<ISender>();
        sender.Setup(x => x.Send(
                It.Is<UserCartQuery>(query => query.CartId == cartId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);
        await using var app = await StartApp(sender.Object);

        var response = await app.GetTestClient().GetAsync($"/cart/{cartId}");
        var cart = await response.Content.ReadFromJsonAsync<CartDto>();

        response.EnsureSuccessStatusCode();
        Assert.NotNull(cart);
        Assert.Equal(16m, cart.TotalPrice);
        Assert.Equal("Product", Assert.Single(cart.Products).ProductName);
    }

    [Fact]
    public async Task CreateCart_BindsBodyAndUsesCurrentUserFallback()
    {
        var productId = Guid.NewGuid();
        var returnedCartId = Guid.NewGuid();
        var sender = new Mock<ISender>();
        sender.Setup(x => x.Send(
                It.Is<AddCartCommand>(command =>
                    command.UserId == Guid.Parse("00000000-0000-0000-0000-000000000011") &&
                    command.Products.Single().ProductId == productId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(returnedCartId);
        await using var app = await StartApp(sender.Object);

        var response = await app.GetTestClient().PostAsJsonAsync("/cart/items",
            new AddCartCommand([
                new ProductViewModelInput(productId, 1, 5m, "Product")
            ]));
        var result = await response.Content.ReadFromJsonAsync<Guid>();

        response.EnsureSuccessStatusCode();
        Assert.Equal(returnedCartId, result);
    }

    private static async Task<WebApplication> StartApp(ISender sender)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Testing"
        });
        builder.WebHost.UseTestServer();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddSingleton(sender);
        var app = builder.Build();
        app.MapCartEndpoints();
        await app.StartAsync();
        return app;
    }
}
