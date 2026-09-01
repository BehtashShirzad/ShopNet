using System.Net.Http.Json;
using CatalogService.Api.Routes;
using CatalogService.Application.Features.Product.Commands.CreateProduct;
using CatalogService.Application.Features.Product.Queries.GetProducts;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace CatalogService.IntegrationTests;

[Collection(CatalogContainersCollection.Name)]
public class CatalogEndpointIntegrationTests
{
    [Fact]
    public async Task GetProducts_ReturnsMediatorResultsOverHttp()
    {
        var expected = new CreateProductCommandResponse(
            Guid.NewGuid(), "Laptop", "Description", 1200m);
        var mediator = new Mock<IMediator>();
        mediator.Setup(x => x.Send(
                It.IsAny<GetProductsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([expected]);
        await using var app = await StartApp(mediator.Object);

        var response = await app.GetTestClient().GetAsync("/products");
        var products = await response.Content
            .ReadFromJsonAsync<List<CreateProductCommandResponse>>();

        response.EnsureSuccessStatusCode();
        Assert.NotNull(products);
        Assert.Equal(expected, Assert.Single(products));
    }

    private static async Task<WebApplication> StartApp(IMediator mediator)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Testing"
        });
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton(mediator);
        var app = builder.Build();
        app.MapProductEndpoints();
        await app.StartAsync();
        return app;
    }
}
