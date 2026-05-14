using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using OrderService.Domain.Aggregates;

namespace OrderService.Infrastructure
{
   public class WriteDbContext : DbContext
{
    public DbSet<OrderAggregate> Orders => Set<OrderAggregate>();

    public WriteDbContext(DbContextOptions<WriteDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.ApplyConfigurationsFromAssembly(typeof(WriteDbContext).Assembly);
    }
}
}