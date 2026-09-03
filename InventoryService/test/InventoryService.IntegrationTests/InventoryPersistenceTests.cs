using InventoryService.Application;
using InventoryService.Infrastructure;
using MassTransit.EntityFrameworkCoreIntegration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ShopNet.Contracts.Inventory.V1;

namespace InventoryService.IntegrationTests;

[Collection("Inventory containers")]
public sealed class InventoryPersistenceTests(InventoryContainers containers)
{
    [Fact]
    public async Task Migrations_HaveNoPendingChangesAndEnforceBalances()
    {
        await using var host = await InventoryTestHost.Create(containers);
        var product = await host.Seed();
        await using var scope = host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        Assert.False(db.Database.HasPendingModelChanges());
        await Assert.ThrowsAsync<Microsoft.Data.SqlClient.SqlException>(() =>
            db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE InventoryItems SET ReservedQuantity = 11 WHERE ProductId = {product}"));
        Assert.Equal(0, (await host.Item(product))!.ReservedQuantity);
        Assert.Throws<NotSupportedException>(() => db.SaveChanges());
    }

    [Fact]
    public async Task ReserveAndCommit_ReloadsDomainWithUtcTimesAndPersistsOutbox()
    {
        await using var host = await InventoryTestHost.Create(containers);
        var p1 = await host.Seed();
        var p2 = await host.Seed();
        var request = host.Request(new InventoryLine(p1, 2), new(p2, 3));
        await host.Run(x => x.ReserveAsync(request));
        Assert.Equal(2, (await host.Item(p1))!.ReservedQuantity);
        var reservation = Assert.Single((await host.Item(p1))!.Reservations);
        Assert.Equal(DateTimeKind.Utc, reservation.ReservedAtUtc.Kind);
        Assert.Equal(request.ReservationRequestId, reservation.ReservationRequestId);
        Assert.Equal(1, await host.Pending());
        await host.Run(x => x.CommitAsync(new(request.OrderId, request.ReservationRequestId)));
        await host.Run(x => x.CommitAsync(new(request.OrderId, request.ReservationRequestId)));
        Assert.Equal(8, (await host.Item(p1))!.OnHandQuantity);
        Assert.Equal(7, (await host.Item(p2))!.OnHandQuantity);
        Assert.Equal(0, (await host.Item(p2))!.ReservedQuantity);
        Assert.Equal(AttemptStatus.Committed, (await host.Attempt(request.ReservationRequestId))!.Status);
        Assert.Equal(DateTimeKind.Utc, Assert.Single((await host.Item(p1))!.Reservations).FinalizedAtUtc!.Value.Kind);
    }

    [Fact]
    public async Task MultiLineRejection_NeverPartiallyReservesAndRetryKeepsRejection()
    {
        await using var host = await InventoryTestHost.Create(containers);
        var p1 = await host.Seed();
        var p2 = await host.Seed(0);
        var request = host.Request(new InventoryLine(p1, 4), new(p2, 1));
        await host.Run(x => x.ReserveAsync(request));
        var original = (await host.Attempt(request.ReservationRequestId))!;
        Assert.Equal(AttemptStatus.Rejected, original.Status);
        Assert.Equal(0, (await host.Item(p1))!.ReservedQuantity);
        Assert.Empty((await host.Item(p1))!.Reservations);
        await host.Run(x => x.ReceiveStockAsync(new(p2, 10, Guid.NewGuid())));
        await host.Run(x => x.ReserveAsync(request));
        Assert.Equal(original.EventId, (await host.Attempt(request.ReservationRequestId))!.EventId);
        Assert.Equal(0, (await host.Item(p1))!.ReservedQuantity);
    }

    [Fact]
    public async Task FailureAfterSqlFlush_RollsBackInventoryAndOutboxTogether()
    {
        await using var host = await InventoryTestHost.Create(containers);
        var p = await host.Seed();
        await using (var scope = host.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
            var store = scope.ServiceProvider.GetRequiredService<IInventoryStore>();
            var publisher = scope.ServiceProvider.GetRequiredService<IInventoryEventPublisher>();
            await Assert.ThrowsAsync<InvalidOperationException>(() => store.ExecuteAsync($"product:{p:N}", async () =>
            {
                var item = (await store.FindProductAsync(p, default))!;
                item.ReceiveStock(5, Guid.NewGuid());
                await publisher.PublishAsync(new InventoryCommitted(Guid.NewGuid(), Guid.NewGuid()), default);
                await db.SaveChangesAsync();
                Assert.Equal(1, await db.Set<OutboxMessage>().CountAsync());
                throw new InvalidOperationException("Failure after SQL flush, before transaction commit.");
            }, default));
        }
        Assert.Equal(10, (await host.Item(p))!.OnHandQuantity);
        Assert.Equal(0, await host.Pending());
    }

    [Fact]
    public async Task ConcurrentOrders_CannotOversellLastUnits()
    {
        await using var host = await InventoryTestHost.Create(containers);
        var p = await host.Seed(5);
        var requests = Enumerable.Range(0, 10).Select(_ => host.Request(new InventoryLine(p, 1))).ToArray();
        await Task.WhenAll(requests.Select(r => host.Run(x => x.ReserveAsync(r))));
        var attempts = await Task.WhenAll(requests.Select(r => host.Attempt(r.ReservationRequestId)));
        Assert.Equal(5, attempts.Count(x => x!.Status == AttemptStatus.Reserved));
        Assert.Equal(5, attempts.Count(x => x!.Status == AttemptStatus.Rejected));
        Assert.Equal(5, (await host.Item(p))!.ReservedQuantity);
        Assert.Equal(0, (await host.Item(p))!.AvailableQuantity);
    }

    [Fact]
    public async Task ConcurrentDuplicateRequest_ReservesExactlyOnce()
    {
        await using var host = await InventoryTestHost.Create(containers);
        var p = await host.Seed();
        var request = host.Request(new InventoryLine(p, 3));
        await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => host.Run(x => x.ReserveAsync(request))));
        Assert.Equal(3, (await host.Item(p))!.ReservedQuantity);
        Assert.Single((await host.Item(p))!.Reservations);
        await using var scope = host.Services.CreateAsyncScope();
        var messages = await scope.ServiceProvider.GetRequiredService<InventoryDbContext>().Set<OutboxMessage>().ToListAsync();
        Assert.Equal(5, messages.Count);
        Assert.Single(messages.Select(x => x.MessageId).Distinct());
    }

    [Fact]
    public async Task ConcurrentDifferentAttemptsForOneOrder_OnlyOneCanReserve()
    {
        await using var host = await InventoryTestHost.Create(containers);
        var p1 = await host.Seed();
        var p2 = await host.Seed();
        var first = host.Request(new InventoryLine(p1, 1));
        var second = first with { ReservationRequestId = Guid.NewGuid(), Items = [new(p2, 1)] };
        await Task.WhenAll(host.Run(x => x.ReserveAsync(first)), host.Run(x => x.ReserveAsync(second)));
        Assert.Equal(1, (await host.Item(p1))!.ReservedQuantity + (await host.Item(p2))!.ReservedQuantity);
    }

    [Fact]
    public async Task ExpiryWorkerRacingCommit_ReleasesOnlyOnceAndSurvivesScopeReload()
    {
        await using var host = await InventoryTestHost.Create(containers);
        var p1 = await host.Seed();
        var p2 = await host.Seed();
        var request = host.Request(new InventoryLine(p1, 2), new(p2, 3));
        await host.Run(x => x.ReserveAsync(request));
        host.Clock.Now = request.ExpiresAtUtc;
        await Task.WhenAll(host.Services.GetRequiredService<ReservationExpiryWorker>().RunOnceAsync(default),
            host.Run(x => x.CommitAsync(new(request.OrderId, request.ReservationRequestId))));
        await host.Services.GetRequiredService<ReservationExpiryWorker>().RunOnceAsync(default);
        Assert.Equal(AttemptStatus.Expired, (await host.Attempt(request.ReservationRequestId))!.Status);
        Assert.Equal(10, (await host.Item(p1))!.OnHandQuantity);
        Assert.Equal(0, (await host.Item(p1))!.ReservedQuantity);
        Assert.Equal(0, (await host.Item(p2))!.ReservedQuantity);
    }

    [Fact]
    public async Task ReleaseTombstone_AndNewAttemptSurviveDatabaseReload()
    {
        await using var host = await InventoryTestHost.Create(containers);
        var p = await host.Seed();
        var request = host.Request(new InventoryLine(p, 3));
        await host.Run(x => x.ReleaseAsync(new(request.OrderId, request.ReservationRequestId, InventoryReleaseReason.OrderCancelled)));
        await host.Run(x => x.ReserveAsync(request));
        Assert.Equal(0, (await host.Item(p))!.ReservedQuantity);
        var next = request with { ReservationRequestId = Guid.NewGuid() };
        await host.Run(x => x.ReserveAsync(next));
        await host.Run(x => x.CommitAsync(new(request.OrderId, request.ReservationRequestId)));
        Assert.Equal(3, (await host.Item(p))!.ReservedQuantity);
        Assert.Equal(AttemptStatus.Reserved, (await host.Attempt(next.ReservationRequestId))!.Status);
    }

    [Fact]
    public async Task StockReceipts_AreDeduplicatedAcrossConcurrentDeliveries()
    {
        await using var host = await InventoryTestHost.Create(containers);
        var p = await host.Seed(0);
        var receipt = new ReceiveInventoryStock(p, 8, Guid.NewGuid());
        await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => host.Run(x => x.ReceiveStockAsync(receipt))));
        Assert.Equal(8, (await host.Item(p))!.OnHandQuantity);
        await Assert.ThrowsAsync<ArgumentException>(() => host.Run(x => x.ReceiveStockAsync(receipt with { Quantity = 9 })));
        Assert.Equal(8, (await host.Item(p))!.OnHandQuantity);
    }

    [Fact]
    public async Task RowVersion_RejectsStaleWriter()
    {
        await using var host = await InventoryTestHost.Create(containers);
        var p = await host.Seed();
        await using var first = host.Services.CreateAsyncScope();
        await using var second = host.Services.CreateAsyncScope();
        var db1 = first.ServiceProvider.GetRequiredService<InventoryDbContext>();
        var db2 = second.ServiceProvider.GetRequiredService<InventoryDbContext>();
        var item1 = await db1.InventoryItems.SingleAsync(x => x.ProductId == p);
        var item2 = await db2.InventoryItems.SingleAsync(x => x.ProductId == p);
        item1.ReceiveStock(1, Guid.NewGuid());
        item2.ReceiveStock(2, Guid.NewGuid());
        await db1.SaveChangesAsync();
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => db2.SaveChangesAsync());
        Assert.Equal(11, (await host.Item(p))!.OnHandQuantity);
    }

    [Fact]
    public async Task ConcurrentMultiLineOrders_AreAtomicWithOppositeInputOrder()
    {
        await using var host = await InventoryTestHost.Create(containers);
        var p1 = await host.Seed(5);
        var p2 = await host.Seed(5);
        var first = host.Request(new InventoryLine(p1, 3), new(p2, 3));
        var second = host.Request(new InventoryLine(p2, 3), new(p1, 3));
        await Task.WhenAll(host.Run(x => x.ReserveAsync(first)), host.Run(x => x.ReserveAsync(second)));
        Assert.Equal(3, (await host.Item(p1))!.ReservedQuantity);
        Assert.Equal(3, (await host.Item(p2))!.ReservedQuantity);
        var outcomes = new[] { await host.Attempt(first.ReservationRequestId), await host.Attempt(second.ReservationRequestId) };
        Assert.Single(outcomes, x => x!.Status == AttemptStatus.Reserved);
        Assert.Single(outcomes, x => x!.Status == AttemptStatus.Rejected);
    }

    [Fact]
    public async Task CommitRacingRelease_ProducesOneTerminalState()
    {
        await using var host = await InventoryTestHost.Create(containers);
        var p = await host.Seed();
        var request = host.Request(new InventoryLine(p, 3));
        await host.Run(x => x.ReserveAsync(request));
        await Task.WhenAll(host.Run(x => x.CommitAsync(new(request.OrderId, request.ReservationRequestId))),
            host.Run(x => x.ReleaseAsync(new(request.OrderId, request.ReservationRequestId, InventoryReleaseReason.PaymentFailed))));
        var attempt = (await host.Attempt(request.ReservationRequestId))!;
        Assert.Contains(attempt.Status, new[] { AttemptStatus.Committed, AttemptStatus.Released });
        Assert.Equal(2, attempt.Version);
        Assert.Equal(0, (await host.Item(p))!.ReservedQuantity);
        Assert.Equal(attempt.Status == AttemptStatus.Committed ? 7 : 10, (await host.Item(p))!.OnHandQuantity);
    }
}
