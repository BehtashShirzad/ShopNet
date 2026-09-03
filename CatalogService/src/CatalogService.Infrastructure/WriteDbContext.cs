using Application.Abstractions.Contracts;
using CatalogService.Domain.Aggregates;
using CatalogService.Domain.Entities;
using Domain.Abstractions;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.Infrastructure;

public class WriteDbContext(
    DbContextOptions<WriteDbContext> options,
    IDomainEventBus domainEventBus,
    ICurrentUser currentUser) : DbContext(options), IApplicationDbContext
{
    public DbSet<ProductAggregate> Products => Set<ProductAggregate>();
    public DbSet<CategoryEntity> Categories => Set<CategoryEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<ProductAggregate>().Property(product => product.Price).HasPrecision(18, 2);
        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();
    }

    // All business writes must dispatch domain events and enlist their outbox messages.
    public override int SaveChanges() => SaveChanges(true);

    public override int SaveChanges(bool acceptAllChangesOnSuccess) =>
        throw new NotSupportedException("Use SaveChangesAsync to persist Catalog domain events.");

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        SaveChangesAsync(true, cancellationToken);

    public override async Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        var userId = Guid.TryParse(currentUser.UserId, out var parsed) ? parsed : Guid.Empty;
        var now = DateTime.UtcNow;

        foreach (var entry in ChangeTracker.Entries<Entity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatorId = userId;
                entry.Entity.CreatedAt = now;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.ModifierId = userId;
                entry.Entity.ModifiedAt = now;
            }
        }

        var aggregates = ChangeTracker.Entries<IAggregateRoot>()
            .Select(entry => entry.Entity).ToList();
        var domainEvents = aggregates.SelectMany(aggregate => aggregate.DomainEvents).ToList();

        // Handlers enqueue integration messages through the scoped EF bus outbox.
        // One save/transaction persists both business data and those messages.
        foreach (var domainEvent in domainEvents)
            await domainEventBus.PublishAsync(domainEvent, cancellationToken);

        var result = await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);

        foreach (var aggregate in aggregates)
            aggregate.ClearEvents();

        return result;
    }
}
