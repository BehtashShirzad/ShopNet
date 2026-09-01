using Domain.Abstractions;
using InventoryService.Domain.Enums;

namespace InventoryService.Domain.DomainEvents;

public abstract record InventoryDomainEvent : IDomainEvent
{
    public Guid Id { get; } = IdGenerator.New();

    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

public sealed record InventoryItemCreatedDomainEvent(
    Guid InventoryItemId,
    Guid ProductId,
    int InitialQuantity,
    int ReorderPoint) : InventoryDomainEvent;

public sealed record StockReceivedDomainEvent(
    Guid InventoryItemId,
    Guid ProductId,
    int Quantity,
    Guid ReferenceId,
    int OnHandQuantity,
    int AvailableQuantity) : InventoryDomainEvent;

public sealed record StockAdjustedDomainEvent(
    Guid InventoryItemId,
    Guid ProductId,
    int QuantityDelta,
    StockAdjustmentReason Reason,
    Guid ReferenceId,
    int OnHandQuantity,
    int ReservedQuantity,
    int AvailableQuantity) : InventoryDomainEvent;

public sealed record StockReservedDomainEvent(
    Guid InventoryItemId,
    Guid ProductId,
    Guid ReservationId,
    Guid OrderId,
    int Quantity,
    DateTime ReservedAtUtc,
    DateTime ExpiresAtUtc,
    int AvailableQuantity) : InventoryDomainEvent;

public sealed record StockReservationCommittedDomainEvent(
    Guid InventoryItemId,
    Guid ProductId,
    Guid ReservationId,
    Guid OrderId,
    int Quantity,
    DateTime CommittedAtUtc,
    int OnHandQuantity,
    int ReservedQuantity,
    int AvailableQuantity) : InventoryDomainEvent;

public sealed record StockReservationReleasedDomainEvent(
    Guid InventoryItemId,
    Guid ProductId,
    Guid ReservationId,
    Guid OrderId,
    int Quantity,
    ReservationReleaseReason Reason,
    DateTime ReleasedAtUtc,
    int AvailableQuantity) : InventoryDomainEvent;

public sealed record StockReservationExpiredDomainEvent(
    Guid InventoryItemId,
    Guid ProductId,
    Guid ReservationId,
    Guid OrderId,
    int Quantity,
    DateTime ExpiredAtUtc,
    int AvailableQuantity) : InventoryDomainEvent;

public sealed record ReorderPointChangedDomainEvent(
    Guid InventoryItemId,
    Guid ProductId,
    int PreviousReorderPoint,
    int NewReorderPoint) : InventoryDomainEvent;

public sealed record LowStockReachedDomainEvent(
    Guid InventoryItemId,
    Guid ProductId,
    int AvailableQuantity,
    int ReorderPoint) : InventoryDomainEvent;

public sealed record InventoryItemActivatedDomainEvent(
    Guid InventoryItemId,
    Guid ProductId) : InventoryDomainEvent;

public sealed record InventoryItemDeactivatedDomainEvent(
    Guid InventoryItemId,
    Guid ProductId) : InventoryDomainEvent;