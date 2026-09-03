using System.Text.Json;
using ProductCreatedV1 = ShopNet.Contracts.IntegrationEvents.Catalog.V1.ProductCreated;

namespace BuildingBlocks.UnitTests;

public sealed class ProductCreatedV1ContractTests
{
    [Fact]
    public void ProductCreatedV1_RoundTripsIdentityAndMetadata()
    {
        var message = new ProductCreatedV1(Guid.NewGuid());
        var json = JsonSerializer.Serialize(message);
        var restored = JsonSerializer.Deserialize<ProductCreatedV1>(json);

        Assert.Equal(message, restored);
        Assert.NotEqual(Guid.Empty, message.EventId);
        Assert.Equal(TimeSpan.Zero, message.OccurredOnUtc.Offset);
    }

    [Fact]
    public void ProductCreatedV1_DoesNotClaimStockOwnership()
    {
        var fields = typeof(ProductCreatedV1).GetProperties().Select(x => x.Name).Order().ToArray();

        Assert.Equal(new[] { "EventId", "OccurredOnUtc", "ProductId" }, fields);
    }
}
