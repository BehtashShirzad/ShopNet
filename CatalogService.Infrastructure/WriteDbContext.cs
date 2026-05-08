using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CatalogService.Domain.Aggregates;
using CatalogService.Domain.Contracts;
using CatalogService.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.Infrastructure
{
    public class WriteDbContext:DbContext,IUnitOfWork
    {
        public WriteDbContext(DbContextOptions<WriteDbContext> options)
            : base(options)
        {
              ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.TrackAll;

        }


 
 public async Task<int> PersistAsync(CancellationToken cancellationToken = default)
{
    return await base.SaveChangesAsync(cancellationToken);
}

public async Task<int> PersistTransactionalAsync(CancellationToken cancellationToken = default)
{
    using var transaction = await Database.BeginTransactionAsync(cancellationToken);

    try
    {
        var result = await base.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }
    catch
    {
        await transaction.RollbackAsync(cancellationToken);
        throw;
    }
}


        public DbSet<ProductAggregate> Products { get; set; }
        public DbSet<CategoryEntity> Categories { get; set; }

    }
}