using Domain.Abstractions;
using InventoryService.Application;
using InventoryService.Domain.Aggregates;
using InventoryService.Domain.Entities;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace InventoryService.Infrastructure;

public sealed class InventoryDbContext(DbContextOptions<InventoryDbContext> options) : DbContext(options)
{
    public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();
    public DbSet<StockReservation> StockReservations => Set<StockReservation>();
    public DbSet<ReservationAttempt> ReservationAttempts => Set<ReservationAttempt>();
    public DbSet<StockReceipt> StockReceipts => Set<StockReceipt>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        var item = model.Entity<InventoryItem>();
        item.ToTable("InventoryItems", table =>
        {
            table.HasCheckConstraint("CK_Inventory_Balances",
                "[OnHandQuantity] >= 0 AND [ReservedQuantity] >= 0 AND [ReservedQuantity] <= [OnHandQuantity]");
            table.HasCheckConstraint("CK_Inventory_ReorderPoint", "[ReorderPoint] >= 0");
        });
        item.HasKey(x => x.Id);
        item.Property(x => x.Id).ValueGeneratedNever();
        item.HasIndex(x => x.ProductId).IsUnique();
        item.Ignore(x => x.AvailableQuantity);
        item.Ignore(x => x.DomainEvents);
        item.Property<byte[]>("RowVersion").IsRowVersion();
        item.HasMany(x => x.Reservations).WithOne().HasForeignKey("InventoryItemId")
            .IsRequired().OnDelete(DeleteBehavior.Restrict);
        item.Navigation(x => x.Reservations).HasField("_reservations").UsePropertyAccessMode(PropertyAccessMode.Field);

        var reservation = model.Entity<StockReservation>();
        reservation.ToTable("StockReservations", table =>
            table.HasCheckConstraint("CK_Reservation_Quantity", "[Quantity] > 0"));
        reservation.HasKey(x => x.Id);
        reservation.Property(x => x.Id).ValueGeneratedNever();
        reservation.HasIndex("InventoryItemId", nameof(StockReservation.ReservationRequestId)).IsUnique();
        reservation.Ignore(x => x.IsActive);
        reservation.Ignore(x => x.IsFinalized);

        var attempt = model.Entity<ReservationAttempt>();
        attempt.ToTable("ReservationAttempts");
        attempt.HasKey(x => x.Id);
        attempt.Property(x => x.Id).ValueGeneratedNever();
        attempt.Property(x => x.Fingerprint).HasMaxLength(64);
        attempt.Property(x => x.Reason).HasMaxLength(128);
        attempt.Ignore(x => x.Items);
        attempt.HasIndex(x => new { x.Status, x.ExpiresAtUtc });
        attempt.HasIndex(x => x.OrderId).IsUnique().HasFilter("[Status] IN (1, 3)");
        attempt.Property<byte[]>("RowVersion").IsRowVersion();

        var receipt = model.Entity<StockReceipt>();
        receipt.ToTable("StockReceipts");
        receipt.HasKey(x => x.ReferenceId);
        receipt.Property(x => x.ReferenceId).ValueGeneratedNever();

        // SQL datetime2 does not retain DateTime.Kind, but the domain requires UTC.
        var utc = new ValueConverter<DateTime, DateTime>(x => x, x => DateTime.SpecifyKind(x, DateTimeKind.Utc));
        var nullableUtc = new ValueConverter<DateTime?, DateTime?>(x => x,
            x => x.HasValue ? DateTime.SpecifyKind(x.Value, DateTimeKind.Utc) : null);
        foreach (var entity in model.Model.GetEntityTypes().ToArray())
            foreach (var property in entity.GetProperties())
            {
                if (property.ClrType == typeof(DateTime)) property.SetValueConverter(utc);
                if (property.ClrType == typeof(DateTime?)) property.SetValueConverter(nullableUtc);
            }

        model.AddInboxStateEntity();
        model.AddOutboxMessageEntity();
        model.AddOutboxStateEntity();
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
        => throw new NotSupportedException("Use the asynchronous Inventory transaction boundary.");

    public override int SaveChanges() => SaveChanges(true);

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => SaveChangesAsync(true, cancellationToken);

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<Entity>())
        {
            if (entry.State == EntityState.Added && entry.Entity.CreatedAt == default)
                entry.Entity.CreatedAt = DateTime.UtcNow;
            if (entry.State is EntityState.Added or EntityState.Modified)
                entry.Entity.ModifiedAt = DateTime.UtcNow;
        }
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }
}
