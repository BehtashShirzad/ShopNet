using System.Net.Http.Json;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrderService.Api;
using OrderService.Application.Query.GetOrderById;
using OrderService.Domain.Enums;
using ShopNet.Contracts.SharedDtos;

namespace OrderService.IntegrationTests;

[Collection(OrderContainersCollection.Name)]
public class OrderEndpointIntegrationTests
{
    [Fact]
    public async Task GetOrder_ReturnsSenderResponseOverHttp()
    {
        var orderId = Guid.NewGuid();
        var responseDto = new GetOrderByIdQueryResponse(
            orderId,
            [new ProductDto(Guid.NewGuid(), "Product", 10m, 2)],
            OrderStatus.Pending);
        var sender = new Mock<ISender>();
        sender.Setup(x => x.Send(
                It.Is<GetOrderByIdQuery>(query =>
                    query.OrderId == orderId &&
                    query.UserId == Guid.Parse("00000000-0000-0000-0000-000000000011")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(responseDto);
        await using var app = await StartApp(sender.Object);

        var response = await app.GetTestClient().GetAsync($"/orders/{orderId}");
        var order = await response.Content.ReadFromJsonAsync<GetOrderByIdQueryResponse>();

        response.EnsureSuccessStatusCode();
        Assert.NotNull(order);
        Assert.Equal(responseDto.OrderId, order.OrderId);
        Assert.Equal("Product", Assert.Single(order.ProductDto).ProductName);
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
        app.MapEndpoint();
        await app.StartAsync();
        return app;
    }
}
