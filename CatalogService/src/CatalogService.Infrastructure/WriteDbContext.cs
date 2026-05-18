using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CatalogService.Domain.Aggregates;
 
using CatalogService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Application.Abstractions.Contracts;
using CatalogService.Domain;
using Domain.Abstractions;

namespace CatalogService.Infrastructure
{
    public class WriteDbContext:DbContext ,IApplicationDbContext

    {
        private readonly IDomainEventBus _bus;
        private readonly ICurrentUser _currentUser;
        public WriteDbContext(DbContextOptions<WriteDbContext> options,IDomainEventBus bus,ICurrentUser currentUser)
            : base(options)
        {
       
          _currentUser = currentUser;
              ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.TrackAll;
            _bus = bus;
        }


 
  public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
var userId =Guid.TryParse( _currentUser.UserId,out Guid currentUserId )? currentUserId  : Guid.Empty;

     


 foreach (var entry in ChangeTracker.Entries<Entity>())
        {
            if (entry.Entity is Entity aggregateRoot)
            {
                if (entry.State == EntityState.Added)
                    {
                        aggregateRoot.CreatorId = currentUserId ;
                        aggregateRoot.CreatedAt = DateTime.UtcNow;
                    }
                else if (entry.State == EntityState.Modified)
                {
                    aggregateRoot.ModifierId = currentUserId ;
                    aggregateRoot.ModifiedAt = DateTime.UtcNow;
                }
            }
        }
  
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
        public DbSet<ProductAggregate> Products { get; set; }
        public DbSet<CategoryEntity> Categories { get; set; }
      
    }
}