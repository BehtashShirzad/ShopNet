using System.Net.Http.Json;
using MassTransit;
using Microsoft.AspNetCore.TestHost;
using OrderService.Application.Query.GetOrderById;
using OrderService.Domain.Enums;
using ShopNet.Contracts.IntegrationEvents;
using ShopNet.Contracts.Inventory.V1;
using ShopNet.Contracts.SharedDtos;

namespace OrderService.IntegrationTests;

[Collection(OrderContainersCollection.Name)]
public sealed class OrderInventoryMessagingTests(OrderContainersFixture fixture)
{
    [Fact]
    public async Task CheckoutThroughRealInventory_ReservesThenExpiresAndUpdatesHttpQuery()
    {
        var clock = new OrderTestClock();
        await using var inventory = await RealInventoryRuntime.Create(fixture, clock);
        var product = await inventory.Seed(5);
        await using var order = await OrderInventoryTestHost.Create(fixture, delivery: true,
            commandQueue: inventory.CommandQueue, clock: clock);
        await order.Start(); // Result subscriptions must exist before Inventory delivery.
        await inventory.Start();
        var checkout = new CartCheckedOutEvent(Guid.NewGuid(),
            Guid.Parse("00000000-0000-0000-0000-000000000011"), [new ProductDto(product, "P", 10, 2)], 20);
        await order.Bus.Publish(checkout);
        await OrderInventoryTestHost.Until(async () => (await order.ByCart(checkout.CartId))?.Status == OrderStatus.InventoryReserved);
        var saved = (await order.ByCart(checkout.CartId))!;
        Assert.Equal(2, await inventory.Reserved(product));
        var response = await order.App.GetTestClient().GetFromJsonAsync<GetOrderByIdQueryResponse>($"/orders/{saved.Id}");
        Assert.Equal(saved.InventoryReservationRequestId, response!.InventoryReservationRequestId);
        Assert.Equal(OrderInventoryStatus.Reserved, response.InventoryStatus);
        Assert.Equal(1, response.InventoryReservationVersion);
        clock.Now = saved.InventoryReservationExpiresAtUtc!.Value;
        await inventory.Expire();
        await OrderInventoryTestHost.Until(async () => (await order.ByCart(checkout.CartId))?.InventoryStatus == OrderInventoryStatus.Expired);
        Assert.Equal(0, await inventory.Reserved(product));
        Assert.Equal(OrderStatus.Failed, (await order.ByCart(checkout.CartId))!.Status);
        // An exact checkout replay cannot create another attempt after failure.
        await order.Send(new OrderService.Application.Commands.CreateOrderCommand(
            checkout.CartId, checkout.CustomerId, checkout.Items, checkout.TotalPrice));
        Assert.Equal(saved.InventoryReservationRequestId, (await order.ByCart(checkout.CartId))!.InventoryReservationRequestId);
    }

    [Fact]
    public async Task RealInventoryRejectsWholeOrderWhenOneLineHasNoStock()
    {
        var clock = new OrderTestClock();
        await using var inventory = await RealInventoryRuntime.Create(fixture, clock);
        var stocked = await inventory.Seed(5);
        var empty = await inventory.Seed(0);
        await using var order = await OrderInventoryTestHost.Create(fixture, delivery: true,
            commandQueue: inventory.CommandQueue, clock: clock);
        await order.Start();
        await inventory.Start();
        var checkout = new CartCheckedOutEvent(Guid.NewGuid(), Guid.NewGuid(),
            [new ProductDto(stocked, "P", 10, 2), new ProductDto(empty, "Empty", 5, 1)], 25);
        await order.Bus.Publish(checkout);
        await OrderInventoryTestHost.Until(async () => (await order.ByCart(checkout.CartId))?.Status == OrderStatus.Failed);
        var saved = (await order.ByCart(checkout.CartId))!;
        Assert.Equal(OrderInventoryStatus.Rejected, saved.InventoryStatus);
        Assert.Equal("InsufficientStock", saved.InventoryFailureReason);
        Assert.Equal(0, await inventory.Reserved(stocked));
    }

    [Fact]
    public async Task ConsumerOutboxPausesAndRestartSendsSameReservationCommand()
    {
        string connection;
        string prefix;
        string queue;
        Guid requestId;
        Guid orderId;
        DateTimeOffset deadline;
        await using (var paused = await OrderInventoryTestHost.Create(fixture))
        {
            connection = paused.ConnectionString;
            prefix = paused.Prefix;
            queue = paused.CommandQueue;
            await paused.Start();
            var checkout = OrderInventoryPersistenceTests.Checkout();
            await paused.Bus.Publish(new CartCheckedOutEvent(checkout.CartId, checkout.CustomerId, checkout.Items, checkout.TotalPrice));
            await OrderInventoryTestHost.Until(async () => await paused.ByCart(checkout.CartId) is not null);
            var saved = (await paused.ByCart(checkout.CartId))!;
            requestId = saved.InventoryReservationRequestId!.Value;
            orderId = saved.Id;
            deadline = saved.InventoryReservationExpiresAtUtc!.Value;
            Assert.Equal(2, (await paused.Outbox()).Count);
        }
        await using var probe = new ReserveCommandProbe(fixture.RabbitMqConnectionString, queue);
        await probe.Start();
        await using var resumed = await OrderInventoryTestHost.Create(fixture, delivery: true, connection, prefix, queue);
        await resumed.Start();
        var received = await probe.Next();
        Assert.Equal(orderId, received.Message.OrderId);
        Assert.Equal(requestId, received.Message.ReservationRequestId);
        Assert.Equal(deadline, received.Message.ExpiresAtUtc);
        Assert.Equal(requestId, received.MessageId);
        Assert.Equal(orderId, received.CorrelationId);
        await OrderInventoryTestHost.Until(async () => (await resumed.Outbox()).Count == 0);
    }

    [Fact]
    public async Task CommandRejectionAndUnexpectedCommitNeedAttentionNotPayment()
    {
        await using var order = await OrderInventoryTestHost.Create(fixture);
        var checkout = OrderInventoryPersistenceTests.Checkout();
        await order.Send(checkout);
        await order.Start();
        var saved = (await order.ByCart(checkout.CartId))!;
        await order.Bus.Publish(new InventoryCommandRejected(saved.Id,
            saved.InventoryReservationRequestId!.Value, "Reserve", "RequestIdConflict"));
        await OrderInventoryTestHost.Until(async () => (await order.ByCart(checkout.CartId))?.Status == OrderStatus.RequiresAttention);
        Assert.Equal(0, (await order.ByCart(checkout.CartId))!.InventoryReservationVersion);
        await order.Bus.Publish(new InventoryCommitted(saved.Id, saved.InventoryReservationRequestId.Value)
            { ReservationVersion = 2 });
        await OrderInventoryTestHost.Until(async () => (await order.ByCart(checkout.CartId))?.InventoryStatus == OrderInventoryStatus.Committed);
        Assert.Equal(OrderStatus.RequiresAttention, (await order.ByCart(checkout.CartId))!.Status);
        Assert.Equal("UnexpectedInventoryCommit", (await order.ByCart(checkout.CartId))!.InventoryFailureReason);
        Assert.Equal(2, (await order.Outbox()).Count);
    }
}
