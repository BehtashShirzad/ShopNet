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
            builder.Property(x => x.Id).ValueGeneratedNever();
            builder.Ignore(x => x.DomainEvents);
            builder.Ignore(x => x.TotalPrice);
            builder.Property<byte[]>("RowVersion").IsRowVersion();
            builder.Property(x => x.InventoryFailureReason).HasMaxLength(256);
            builder.HasIndex(x => x.CartId).IsUnique()
                .HasFilter("[CartId] <> '00000000-0000-0000-0000-000000000000'");

            builder.OwnsMany(x => x.Items, b =>
            {
                b.WithOwner().HasForeignKey("OrderId");
                b.Property<Guid>("Id");
                b.HasKey("Id");
                b.Property(x => x.Price).HasPrecision(18, 2);
            });
            builder.Navigation(x => x.Items).HasField("_items").UsePropertyAccessMode(PropertyAccessMode.Field);
        }
    }
}
