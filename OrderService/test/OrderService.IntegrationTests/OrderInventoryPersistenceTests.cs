using Application.Abstractions.Contracts;
using Domain.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using OrderService.Application.Commands;
using OrderService.Application.Inventory;
using OrderService.Domain.Enums;
using OrderService.Infrastructure;
using ShopNet.Contracts.Inventory.V1;
using ShopNet.Contracts.SharedDtos;

namespace OrderService.IntegrationTests;

[Collection(OrderContainersCollection.Name)]
public sealed class OrderInventoryPersistenceTests(OrderContainersFixture fixture)
{
    internal static CreateOrderCommand Checkout() => new(Guid.NewGuid(), Guid.NewGuid(),
        [new ProductDto(Guid.NewGuid(), "Product", 10, 2)], 20);

    [Fact]
    public async Task CreateAndReplay_PersistOneOrderAndTwoOutboxMessages()
    {
        await using var host = await OrderInventoryTestHost.Create(fixture);
        var command = Checkout();
        await host.Send(command);
        await host.Send(command);
        var order = (await host.ByCart(command.CartId))!;
        Assert.Equal(OrderInventoryStatus.Requested, order.InventoryStatus);
        Assert.Equal(OrderStatus.Pending, order.Status);
        Assert.NotEqual(Guid.Empty, order.InventoryReservationRequestId);
        Assert.Equal(host.Clock.Now.AddMinutes(15), order.InventoryReservationExpiresAtUtc);
        var messages = await host.Outbox();
        Assert.Equal(2, messages.Count);
        var reserve = Assert.Single(messages, x => x.MessageType.Contains(nameof(ReserveInventory)));
        Assert.Equal(order.InventoryReservationRequestId, reserve.MessageId);
        Assert.Equal(order.Id, reserve.CorrelationId);
        Assert.Contains(host.CommandQueue, reserve.DestinationAddress!.ToString());
    }

    [Fact]
    public async Task ConcurrentCheckoutsForSameCart_AreDeduplicatedInSql()
    {
        await using var host = await OrderInventoryTestHost.Create(fixture);
        var command = Checkout();
        await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => host.Send(command)));
        Assert.NotNull(await host.ByCart(command.CartId));
        Assert.Equal(2, (await host.Outbox()).Count);
    }

    [Fact]
    public async Task ChangedCheckoutOrInvalidTotalDoesNotQueueAnotherReservation()
    {
        await using var host = await OrderInventoryTestHost.Create(fixture);
        var command = Checkout();
        await host.Send(command);
        await Assert.ThrowsAsync<ArgumentException>(() => host.Send(command with { CustomerId = Guid.NewGuid() }));
        await Assert.ThrowsAsync<ArgumentException>(() => host.Send(command with { TotalPrice = 999 }));
        Assert.Equal(2, (await host.Outbox()).Count);
    }

    [Fact]
    public async Task OuterRollbackAfterSqlFlush_RemovesOrderAndBothMessages()
    {
        await using var host = await OrderInventoryTestHost.Create(fixture);
        var command = Checkout();
        await using (var scope = host.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WriteDbContext>();
            await using var transaction = await db.Database.BeginTransactionAsync();
            await scope.ServiceProvider.GetRequiredService<ISender>().Send(command);
            await db.SaveChangesAsync(true);
            Assert.Equal(1, await db.Orders.CountAsync());
            Assert.Equal(2, await db.Set<MassTransit.EntityFrameworkCoreIntegration.OutboxMessage>().CountAsync());
            await transaction.RollbackAsync();
        }
        Assert.Null(await host.ByCart(command.CartId));
        Assert.Empty(await host.Outbox());
    }

    [Fact]
    public async Task RealSqlInsertFailure_DoesNotLeavePhantomCommands()
    {
        await using var host = await OrderInventoryTestHost.Create(fixture);
        await using (var scope = host.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<WriteDbContext>().Database.ExecuteSqlRawAsync("""
                CREATE TRIGGER RejectOrderTestInsert ON Orders AFTER INSERT AS
                BEGIN
                    THROW 51001, 'Injected integration-test insert failure.', 1;
                END
                """);
        var command = Checkout();
        await Assert.ThrowsAsync<DbUpdateException>(() => host.Send(command));
        Assert.Null(await host.ByCart(command.CartId));
        Assert.Empty(await host.Outbox());
    }

    [Fact]
    public async Task ExpiredResultBeforeReserved_CannotBeReversedAfterReload()
    {
        await using var host = await OrderInventoryTestHost.Create(fixture);
        var command = Checkout();
        await host.Send(command);
        var order = (await host.ByCart(command.CartId))!;
        await host.Send(new ApplyInventoryResultCommand(order.Id, order.InventoryReservationRequestId!.Value,
            2, OrderInventoryStatus.Expired, "DeadlineElapsed"));
        await host.Send(new ApplyInventoryResultCommand(order.Id, order.InventoryReservationRequestId.Value,
            1, OrderInventoryStatus.Reserved, Items: [new(command.Items[0].ProductId, 2)],
            ExpiresAtUtc: order.InventoryReservationExpiresAtUtc));
        var reloaded = (await host.ByCart(command.CartId))!;
        Assert.Equal(OrderStatus.Failed, reloaded.Status);
        Assert.Equal(2, reloaded.InventoryReservationVersion);
        Assert.Equal(OrderInventoryStatus.Expired, reloaded.InventoryStatus);
        Assert.Equal(2, (await host.Outbox()).Count); // No payment/commit command was introduced.
    }

    [Fact]
    public async Task ConcurrentReservedAndExpiredResults_EndWithExpired()
    {
        await using var host = await OrderInventoryTestHost.Create(fixture);
        var command = Checkout();
        await host.Send(command);
        var order = (await host.ByCart(command.CartId))!;
        await Task.WhenAll(host.Send(new ApplyInventoryResultCommand(order.Id, order.InventoryReservationRequestId!.Value,
            2, OrderInventoryStatus.Expired, "Expired")),
            host.Send(new ApplyInventoryResultCommand(order.Id, order.InventoryReservationRequestId.Value,
                1, OrderInventoryStatus.Reserved, Items: [new(command.Items[0].ProductId, 2)],
                ExpiresAtUtc: order.InventoryReservationExpiresAtUtc)));
        Assert.Equal(OrderInventoryStatus.Expired, (await host.ByCart(command.CartId))!.InventoryStatus);
    }

    [Fact]
    public async Task UnitOfWorkSave_UsesSameContextAndDispatchesOutbox()
    {
        await using var host = await OrderInventoryTestHost.Create(fixture);
        await using var scope = host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<WriteDbContext>();
        Assert.Same(db, scope.ServiceProvider.GetRequiredService<IApplicationDbContext>());
        Assert.Same(db, scope.ServiceProvider.GetRequiredService<IUnitOfWork>());
        await using var transaction = await db.Database.BeginTransactionAsync();
        await scope.ServiceProvider.GetRequiredService<ISender>().Send(Checkout());
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync();
        await transaction.CommitAsync();
        Assert.Equal(2, (await host.Outbox()).Count);
    }

    [Fact]
    public async Task MigrationPreservesLegacyOrdersWithoutRetroactiveReservation()
    {
        await using var host = await OrderInventoryTestHost.Create(fixture, migrate: false);
        var orderId = Guid.NewGuid();
        var cartId = Guid.NewGuid();
        await using var scope = host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<WriteDbContext>();
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("20260518220751_CartId");
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO Orders (Id, CustomerId, CartId, Status, CreatedAt, CreatorId, ModifiedAt, ModifierId)
            VALUES ({orderId}, {Guid.NewGuid()}, {cartId}, 1, SYSUTCDATETIME(), {Guid.Empty}, SYSUTCDATETIME(), {Guid.Empty})
            """);
        await db.Database.MigrateAsync();
        var order = (await host.ByCart(cartId))!;
        Assert.Equal(orderId, order.Id);
        Assert.Null(order.InventoryReservationRequestId);
        Assert.Null(order.InventoryStatus);
        Assert.Empty(await host.Outbox());
        Assert.False(db.Database.HasPendingModelChanges());
    }

    [Fact]
    public async Task DuplicateHistoricalCartIdsBlockMigrationWithoutDeletingOrders()
    {
        await using var host = await OrderInventoryTestHost.Create(fixture, migrate: false);
        var cart = Guid.NewGuid();
        await using var scope = host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<WriteDbContext>();
        await db.GetService<IMigrator>().MigrateAsync("20260518220751_CartId");
        for (var index = 0; index < 2; index++)
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO Orders (Id, CustomerId, CartId, Status, CreatedAt, CreatorId, ModifiedAt, ModifierId)
                VALUES ({Guid.NewGuid()}, {Guid.NewGuid()}, {cart}, 1, SYSUTCDATETIME(), {Guid.Empty}, SYSUTCDATETIME(), {Guid.Empty})
                """);
        await Assert.ThrowsAsync<Microsoft.Data.SqlClient.SqlException>(() => db.Database.MigrateAsync());
        Assert.Equal(2, await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM Orders WHERE CartId = {cart}").SingleAsync());
    }

    [Fact]
    public async Task RowVersionRejectsStaleOrderWriter()
    {
        await using var host = await OrderInventoryTestHost.Create(fixture);
        var command = Checkout();
        await host.Send(command);
        await using var first = host.Services.CreateAsyncScope();
        await using var second = host.Services.CreateAsyncScope();
        var db1 = first.ServiceProvider.GetRequiredService<WriteDbContext>();
        var db2 = second.ServiceProvider.GetRequiredService<WriteDbContext>();
        var order1 = await db1.Orders.SingleAsync(x => x.CartId == command.CartId);
        var order2 = await db2.Orders.SingleAsync(x => x.CartId == command.CartId);
        order1.ApplyInventoryResult(order1.InventoryReservationRequestId!.Value, 1, OrderInventoryStatus.Reserved);
        order2.ApplyInventoryResult(order2.InventoryReservationRequestId!.Value, 2, OrderInventoryStatus.Expired);
        await db1.SaveChangesAsync();
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => db2.SaveChangesAsync());
        Assert.Equal(OrderInventoryStatus.Reserved, (await host.ByCart(command.CartId))!.InventoryStatus);
    }
}
