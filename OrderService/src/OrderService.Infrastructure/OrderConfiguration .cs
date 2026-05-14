using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderService.Domain.Aggregates;

namespace OrderService.Infrastructure
{
    public class OrderConfiguration : IEntityTypeConfiguration<OrderAggregate>
    {
        public void Configure(EntityTypeBuilder<OrderAggregate> builder)
        {
            builder.HasKey(x => x.Id);

            builder.OwnsMany(x => x.Items, b =>
            {
                b.WithOwner().HasForeignKey("OrderId");
                b.Property<Guid>("Id");
                b.HasKey("Id");
            });
        }
    }
}