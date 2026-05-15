using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Application.Abstractions.Contracts;
using Domain.Abstractions;
using Microsoft.EntityFrameworkCore;
using OrderService.Domain.Aggregates;

namespace OrderService.Infrastructure
{
   public class WriteDbContext : DbContext,IUnitOfWork
{
    public DbSet<OrderAggregate> Orders => Set<OrderAggregate>();

 private readonly IDomainEventBus _bus;
    public WriteDbContext(DbContextOptions<WriteDbContext> options, IDomainEventBus bus)
        : base(options)
    {
        _bus = bus;
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.ApplyConfigurationsFromAssembly(typeof(WriteDbContext).Assembly);
    }



  public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
 

     

 
  
var domainEvents = ChangeTracker.Entries()
    .Where(e => e.Entity is IAggregateRoot)
    .SelectMany(e => ((IAggregateRoot)e.Entity).DomainEvents)
    .ToList();


        var result = await base.SaveChangesAsync(cancellationToken);

        foreach (var domainEvent in domainEvents)
        {
            await _bus.PublishAsync(domainEvent, cancellationToken);
        }

        foreach (var entry in ChangeTracker.Entries<IAggregateRoot>())
        {
            entry.Entity.ClearEvents();
        }

        return result;
    }

        Task IUnitOfWork.SaveChangesAsync()
        {
            return base.SaveChangesAsync();
        }
    }
}