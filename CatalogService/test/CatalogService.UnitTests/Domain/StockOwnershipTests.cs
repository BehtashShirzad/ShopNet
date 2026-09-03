using CatalogService.API.Grpc.Protos;
using CatalogService.Application.Features.Product.Commands.CreateProduct;
using CatalogService.Application.Features.Product.Queries.GetProduct;
using CatalogService.Domain.Aggregates;

namespace CatalogService.UnitTests;

public sealed class StockOwnershipTests
{
    [Theory]
    [InlineData(typeof(ProductAggregate))]
    [InlineData(typeof(CreateProductCommand))]
    [InlineData(typeof(CreateProductCommandResponse))]
    [InlineData(typeof(GetProductDto))]
    [InlineData(typeof(ProductResponse))]
    public void CatalogPublicModelsDoNotExposeStock(Type type)
        => Assert.Null(type.GetProperty("Stock"));

    [Fact]
    public void LegacyGrpcFieldNumberIsNotAssignedToAnotherField()
    {
        Assert.Null(ProductResponse.Descriptor.FindFieldByNumber(5));
        Assert.Equal(new[] { 1, 2, 4 }, ProductResponse.Descriptor.Fields.InFieldNumberOrder()
            .Select(field => field.FieldNumber));
    }

    [Fact]
    public void CheckedInSettingsExplicitlyPauseCatalogOutboxAndAreValidJson()
    {
        using var settings = System.Text.Json.JsonDocument.Parse(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "appsettings.json")));
        Assert.False(settings.RootElement.GetProperty("CatalogOutbox")
            .GetProperty("DeliveryEnabled").GetBoolean());

        using var production = System.Text.Json.JsonDocument.Parse(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "appsettings.Production.json")));
        Assert.Equal(System.Text.Json.JsonValueKind.Object, production.RootElement.ValueKind);
    }
}
