using Application.Abstractions.Contracts;
using Domain.Abstractions;
using Microsoft.EntityFrameworkCore;
using OrderService.Domain.Aggregates;
using OrderService.Domain.DomanEvents;
using OrderService.Infrastructure;

namespace OrderService.IntegrationTests;

[Collection(OrderContainersCollection.Name)]
public class OrderPersistenceIntegrationTests(OrderContainersFixture fixture)
{
    [Fact]
    public async Task Repository_PersistsOwnedItemsAndDbContextDispatchesEvents()
    {
        var domainEventBus = new RecordingDomainEventBus();
        var options = new DbContextOptionsBuilder<WriteDbContext>()
            .UseSqlServer(fixture.DatabaseConnectionString)
            .Options;
        await using var context = new WriteDbContext(options, domainEventBus);
        await context.Database.MigrateAsync();
        var order = OrderAggregate.Create(Guid.NewGuid(), Guid.NewGuid());
        order.AddItem(Guid.NewGuid(), "First", 10m, 1);
        order.AddItem(Guid.NewGuid(), "Second", 20m, 2);
        order.RaiseEvent(new OrderCreatedDomainEvent(
            order.Id, order.Items.ToList(), order.CustomerId));
        var repository = new OrderRepository(context);

        await repository.AddAsync(order);
        await context.SaveChangesAsync();

        Assert.Single(domainEventBus.Events);
        Assert.Empty(order.DomainEvents);

        await using var verificationContext = new WriteDbContext(options, new RecordingDomainEventBus());
        var loaded = await new OrderRepository(verificationContext).GetByIdAsync(order.Id);

        Assert.NotNull(loaded);
        Assert.Equal(order.CartId, loaded.CartId);
        Assert.Equal(50m, loaded.TotalPrice);
        Assert.Equal(2, loaded.Items.Count);
        Assert.NotNull(await new OrderRepository(verificationContext)
            .GetByCartId(order.CartId));
    }

    private sealed class RecordingDomainEventBus : IDomainEventBus
    {
        public List<IDomainEvent> Events { get; } = [];

        public Task PublishAsync<T>(T domainEvent, CancellationToken cancellationToken = default)
            where T : IDomainEvent
        {
            Events.Add(domainEvent);
            return Task.CompletedTask;
        }
    }
}
