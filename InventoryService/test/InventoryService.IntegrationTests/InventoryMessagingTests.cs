using InventoryService.Application;
using ShopNet.Contracts.Inventory.V1;
using ProductCreated = ShopNet.Contracts.IntegrationEvents.Catalog.V1.ProductCreated;

namespace InventoryService.IntegrationTests;

[Collection("Inventory containers")]
public sealed class InventoryMessagingTests(InventoryContainers containers)
{
    [Fact]
    public async Task RabbitMq_ProductRegistrationReceiptReserveCommitAndReplay()
    {
        await using var probe = new InventoryMessageProbe(containers.Rabbit);
        await probe.Start();
        await using var host = await InventoryTestHost.Create(containers, deliver: true);
        await host.Start();
        var product = Guid.NewGuid();
        await probe.Publish(new ProductCreated(product));
        await InventoryTestHost.Until(async () => await host.Item(product) is not null);
        Assert.Equal(0, (await host.Item(product))!.OnHandQuantity);
        var receipt = new ReceiveInventoryStock(product, 5, Guid.NewGuid());
        await probe.Send(host.Prefix + InventoryQueues.StockReceipts, receipt);
        await InventoryTestHost.Until(async () => (await host.Item(product))!.OnHandQuantity == 5);
        await probe.Publish(new ProductCreated(product));
        await probe.Send(host.Prefix + InventoryQueues.StockReceipts, receipt);
        var request = host.Request(new InventoryLine(product, 2));
        await probe.Send(host.Prefix + InventoryQueues.Commands, request);
        var reserved = await probe.Next<InventoryReserved>(x => x.ReservationRequestId == request.ReservationRequestId);
        Assert.Equal(request.OrderId, reserved.OrderId);
        Assert.Equal(1, reserved.ReservationVersion);
        Assert.Equal(request.Items, reserved.Items);
        await probe.Send(host.Prefix + InventoryQueues.Commands, request);
        var replay = await probe.Next<InventoryReserved>(x => x.ReservationRequestId == request.ReservationRequestId);
        Assert.Equal(reserved.EventId, replay.EventId);
        await probe.Send(host.Prefix + InventoryQueues.Commands, new CommitInventory(request.OrderId, request.ReservationRequestId));
        Assert.Equal(2, (await probe.Next<InventoryCommitted>(x => x.ReservationRequestId == request.ReservationRequestId)).ReservationVersion);
        Assert.Equal(3, (await host.Item(product))!.OnHandQuantity);
        Assert.Equal(0, (await host.Item(product))!.ReservedQuantity);
        await InventoryTestHost.Until(async () => await host.Pending() == 0);
    }

    [Fact]
    public async Task ConsumerResults_StayInSqlWhileDeliveryPausedAndDrainAfterRestart()
    {
        string connection;
        string prefix;
        Guid eventId;
        ReserveInventory request;
        await using var probe = new InventoryMessageProbe(containers.Rabbit);
        await probe.Start();
        await using (var paused = await InventoryTestHost.Create(containers))
        {
            connection = paused.ConnectionString;
            prefix = paused.Prefix;
            var product = await paused.Seed();
            request = paused.Request(new InventoryLine(product, 4));
            await paused.Start();
            await probe.Send(prefix + InventoryQueues.Commands, request);
            await InventoryTestHost.Until(async () => await paused.Attempt(request.ReservationRequestId) is not null);
            eventId = (await paused.Attempt(request.ReservationRequestId))!.EventId;
            Assert.Equal(1, await paused.Pending()); // verifies the consumer uses SQL outbox, not ConsumeContext.Publish.
        }
        await using var resumed = await InventoryTestHost.Create(containers, deliver: true, connection, prefix);
        await resumed.Start();
        var result = await probe.Next<InventoryReserved>(x => x.ReservationRequestId == request.ReservationRequestId);
        Assert.Equal(eventId, result.EventId);
        await InventoryTestHost.Until(async () => await resumed.Pending() == 0);
    }

    [Fact]
    public async Task RabbitMq_RejectionReleaseAndCompensationTombstone()
    {
        await using var probe = new InventoryMessageProbe(containers.Rabbit);
        await probe.Start();
        await using var host = await InventoryTestHost.Create(containers, deliver: true);
        var product = await host.Seed(0);
        await host.Start();
        var rejected = host.Request(new InventoryLine(product, 1));
        await probe.Send(host.Prefix + InventoryQueues.Commands, rejected);
        Assert.Equal("InsufficientStock",
            (await probe.Next<InventoryRejected>(x => x.ReservationRequestId == rejected.ReservationRequestId)).Reason);
        var cancelled = host.Request(new InventoryLine(product, 1));
        await probe.Send(host.Prefix + InventoryQueues.Commands,
            new ReleaseInventory(cancelled.OrderId, cancelled.ReservationRequestId, InventoryReleaseReason.OrderCancelled));
        var released = await probe.Next<InventoryReleased>(x => x.ReservationRequestId == cancelled.ReservationRequestId);
        await probe.Send(host.Prefix + InventoryQueues.Commands, cancelled);
        Assert.Equal(released.EventId,
            (await probe.Next<InventoryReleased>(x => x.ReservationRequestId == cancelled.ReservationRequestId)).EventId);
        Assert.Equal(0, (await host.Item(product))!.ReservedQuantity);
    }

    [Fact]
    public async Task ExpirationAndInvalidCommand_AreDeliveredAsDistinctResults()
    {
        await using var probe = new InventoryMessageProbe(containers.Rabbit);
        await probe.Start();
        await using var host = await InventoryTestHost.Create(containers, deliver: true);
        var product = await host.Seed();
        await host.Start();
        var request = host.Request(new InventoryLine(product, 2));
        await probe.Send(host.Prefix + InventoryQueues.Commands, request);
        await probe.Next<InventoryReserved>(x => x.ReservationRequestId == request.ReservationRequestId);
        host.Clock.Now = request.ExpiresAtUtc;
        await probe.Send(host.Prefix + InventoryQueues.Commands, new CommitInventory(request.OrderId, request.ReservationRequestId));
        Assert.Equal(2, (await probe.Next<InventoryExpired>(x => x.ReservationRequestId == request.ReservationRequestId)).ReservationVersion);
        var missing = new CommitInventory(Guid.NewGuid(), Guid.NewGuid());
        await probe.Send(host.Prefix + InventoryQueues.Commands, missing);
        Assert.Equal("ReservationNotFound", (await probe.Next<InventoryCommandRejected>(x => x.ReservationRequestId == missing.ReservationRequestId)).Reason);
        Assert.Equal(10, (await host.Item(product))!.OnHandQuantity);
    }
}
