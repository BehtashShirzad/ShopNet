using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.Infrastructure
{
    public class QueryDbContext:DbContext
    {
        public QueryDbContext(DbContextOptions<QueryDbContext> options)
            : base(options)
        {
             ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
        }

        override protected void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
          
        }

        public DbSet<Domain.Aggregates.ProductAggregate> Products { get; set; }
        public DbSet<Domain.Entities.CategoryEntity> Categories { get; set; }
    }
}