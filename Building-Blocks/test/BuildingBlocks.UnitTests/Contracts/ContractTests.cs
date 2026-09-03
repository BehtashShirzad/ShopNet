using ShopNet.Contracts.IntegrationEvents;
using ShopNet.Contracts.Interfaces;
using ShopNet.Contracts.SharedDtos;

namespace BuildingBlocks.UnitTests;

public class ContractTests
{
    [Fact]
    public void CartCheckedOutEvent_CarriesPayloadAndMetadata()
    {
        var before = DateTimeOffset.UtcNow;
        var item = new ProductDto(Guid.NewGuid(), "Keyboard", 125.50m, 2);

        var message = new CartCheckedOutEvent(
            Guid.NewGuid(), Guid.NewGuid(), [item], 251m);

        Assert.IsAssignableFrom<IIntegrationEvent>(message);
        Assert.NotEqual(Guid.Empty, message.EventId);
        Assert.True(message.OccurredOnUtc >= before);
        Assert.Equal(item, Assert.Single(message.Items));
        Assert.Equal(251m, message.TotalPrice);
    }

    [Fact]
    public void OrderCreatedEvent_CarriesOrderItems()
    {
        var item = new ProductDto(Guid.NewGuid(), "Mouse", 42m, 1);
        var orderId = Guid.NewGuid();
        var customerId = Guid.NewGuid();

        var message = new OrderCreatedEvent(orderId, customerId, [item]);

        Assert.Equal(orderId, message.OrderId);
        Assert.Equal(customerId, message.CustomerId);
        Assert.Equal(item, Assert.Single(message.Items));
        Assert.NotEqual(Guid.Empty, message.EventId);
    }

    [Fact]
    public void ProductDto_UsesValueEquality()
    {
        var id = Guid.NewGuid();

        Assert.Equal(
            new ProductDto(id, "Monitor", 300m, 2),
            new ProductDto(id, "Monitor", 300m, 2));
    }
}
