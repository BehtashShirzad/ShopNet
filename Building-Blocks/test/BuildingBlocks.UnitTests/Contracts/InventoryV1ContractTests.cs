using System.Text.Json;
using ShopNet.Contracts;
using ShopNet.Contracts.Inventory.V1;

namespace BuildingBlocks.UnitTests;

public sealed class InventoryV1ContractTests
{
    [Fact]
    public void ReserveCommand_RoundTripsCorrelationAndLineItems()
    {
        var source = new ReserveInventory(Guid.NewGuid(), Guid.NewGuid(),
            [new(Guid.NewGuid(), 3)], DateTimeOffset.UtcNow.AddMinutes(10));
        var result = JsonSerializer.Deserialize<ReserveInventory>(JsonSerializer.Serialize(source))!;
        Assert.Equal(source.OrderId, result.OrderId);
        Assert.Equal(source.ReservationRequestId, result.ReservationRequestId);
        Assert.Equal(source.ExpiresAtUtc, result.ExpiresAtUtc);
        Assert.Equal(source.Items, result.Items);
    }

    [Fact]
    public void EveryOutcome_RoundTripsIdentityMetadataAndCorrelation()
    {
        var order = Guid.NewGuid();
        var request = Guid.NewGuid();
        IntegrationEvent[] events = [
            new InventoryReserved(order, request, [new(Guid.NewGuid(), 1)], DateTimeOffset.UtcNow.AddMinutes(10)),
            new InventoryRejected(order, request, "InsufficientStock"),
            new InventoryCommitted(order, request), new InventoryReleased(order, request),
            new InventoryExpired(order, request), new InventoryCommandRejected(order, request, "Commit", "NotFound")];
        foreach (var source in events)
        {
            var json = JsonSerializer.Serialize(source, source.GetType());
            var result = (IntegrationEvent)JsonSerializer.Deserialize(json, source.GetType())!;
            Assert.Equal(source.EventId, result.EventId);
            Assert.Equal(source.OccurredOnUtc, result.OccurredOnUtc);
            Assert.Equal(order, result.GetType().GetProperty("OrderId")!.GetValue(result));
            Assert.Equal(request, result.GetType().GetProperty("ReservationRequestId")!.GetValue(result));
        }
    }

    [Fact]
    public void FinalizationAndReceiptCommands_RoundTrip()
    {
        object[] commands = [new CommitInventory(Guid.NewGuid(), Guid.NewGuid()),
            new ReleaseInventory(Guid.NewGuid(), Guid.NewGuid(), InventoryReleaseReason.PaymentFailed),
            new ReceiveInventoryStock(Guid.NewGuid(), 10, Guid.NewGuid())];
        foreach (var command in commands)
            Assert.Equal(command, JsonSerializer.Deserialize(
                JsonSerializer.Serialize(command, command.GetType()), command.GetType()));
    }
}
