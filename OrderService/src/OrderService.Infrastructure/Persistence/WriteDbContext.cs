using Application.Abstractions.Contracts;
using Domain.Abstractions;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using OrderService.Domain.Aggregates;

namespace OrderService.Infrastructure;

public class WriteDbContext(DbContextOptions<WriteDbContext> options, IDomainEventBus bus)
    : DbContext(options), IUnitOfWork, IApplicationDbContext
{
    public DbSet<OrderAggregate> Orders => Set<OrderAggregate>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.ApplyConfigurationsFromAssembly(typeof(WriteDbContext).Assembly);
        builder.AddInboxStateEntity();
        builder.AddOutboxMessageEntity();
        builder.AddOutboxStateEntity();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => SaveChangesAsync(true, cancellationToken);

    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        var aggregates = ChangeTracker.Entries<IAggregateRoot>()
            .Select(x => x.Entity).ToArray();
        var events = aggregates.SelectMany(x => x.DomainEvents).ToArray();
        // Handlers enqueue through scoped BusOutbox before the same SQL save/transaction.
        foreach (var domainEvent in events)
            await bus.PublishAsync(domainEvent, cancellationToken);
        var result = await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        foreach (var aggregate in aggregates) aggregate.ClearEvents();
        return result;
    }

    public override int SaveChanges() => SaveChanges(true);
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
        => throw new NotSupportedException("Use asynchronous Order saves to preserve transactional messaging.");

    async Task IUnitOfWork.SaveChangesAsync() => await SaveChangesAsync();
}
