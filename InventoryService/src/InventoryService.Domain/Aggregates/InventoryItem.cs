using Domain.Abstractions;
using InventoryService.Domain.DomainEvents;
using InventoryService.Domain.Entities;
using InventoryService.Domain.Enums;

namespace InventoryService.Domain.Aggregates;

public sealed class InventoryItem : AggregateRoot<Guid>
{
    private readonly List<StockReservation> _reservations = [];

    // Required by EF Core
    private InventoryItem()
    {
    }

    private InventoryItem(
        Guid id,
        Guid productId,
        int initialQuantity,
        int reorderPoint)
        : base(id)
    {
        ProductId = productId;
        OnHandQuantity = initialQuantity;
        ReservedQuantity = 0;
        ReorderPoint = reorderPoint;
        IsActive = true;
    }

    public Guid ProductId { get; private set; }

    public int OnHandQuantity { get; private set; }

    public int ReservedQuantity { get; private set; }

    public int AvailableQuantity =>
        OnHandQuantity - ReservedQuantity;

    public int ReorderPoint { get; private set; }

    public bool IsActive { get; private set; }

    public IReadOnlyCollection<StockReservation> Reservations =>
        _reservations.AsReadOnly();

    public static InventoryItem Create(
        Guid productId,
        int initialQuantity = 0,
        int reorderPoint = 0)
    {
        if (productId == Guid.Empty)
        {
            throw new DomainException(
                "ProductId cannot be empty.");
        }

        if (initialQuantity < 0)
        {
            throw new DomainException(
                "Initial quantity cannot be negative.");
        }

        if (reorderPoint < 0)
        {
            throw new DomainException(
                "Reorder point cannot be negative.");
        }

        var inventoryItem = new InventoryItem(
            IdGenerator.New(),
            productId,
            initialQuantity,
            reorderPoint);

        inventoryItem.RaiseEvent(
            new InventoryItemCreatedDomainEvent(
                inventoryItem.Id,
                productId,
                initialQuantity,
                reorderPoint));

        return inventoryItem;
    }

    public void ReceiveStock(
        int quantity,
        Guid referenceId)
    {
        EnsureActive();
        EnsureReferenceId(referenceId);

        if (quantity <= 0)
        {
            throw new DomainException(
                "Received quantity must be greater than zero.");
        }

        OnHandQuantity = checked(OnHandQuantity + quantity);

        RaiseEvent(new StockReceivedDomainEvent(
            Id,
            ProductId,
            quantity,
            referenceId,
            OnHandQuantity,
            AvailableQuantity));
    }

    public void AdjustStock(
        int quantityDelta,
        StockAdjustmentReason reason,
        Guid referenceId)
    {
        EnsureActive();
        EnsureReferenceId(referenceId);

        if (quantityDelta == 0)
        {
            throw new DomainException(
                "Stock adjustment cannot be zero.");
        }

        if (reason == StockAdjustmentReason.None)
        {
            throw new DomainException(
                "Stock adjustment reason is required.");
        }

        var previousAvailableQuantity = AvailableQuantity;

        var adjustedOnHandQuantity =
            checked(OnHandQuantity + quantityDelta);

        if (adjustedOnHandQuantity < 0)
        {
            throw new DomainException(
                "Stock adjustment cannot make OnHandQuantity negative.");
        }

        if (adjustedOnHandQuantity < ReservedQuantity)
        {
            throw new DomainException(
                "Stock adjustment cannot reduce OnHandQuantity " +
                "below ReservedQuantity.");
        }

        OnHandQuantity = adjustedOnHandQuantity;

        RaiseEvent(new StockAdjustedDomainEvent(
            Id,
            ProductId,
            quantityDelta,
            reason,
            referenceId,
            OnHandQuantity,
            ReservedQuantity,
            AvailableQuantity));

        RaiseLowStockEventIfNeeded(previousAvailableQuantity);
    }

    public StockReservation Reserve(
        Guid orderId,
        int quantity,
        DateTime expiresAtUtc)
    {
        return Reserve(
            orderId,
            quantity,
            DateTime.UtcNow,
            expiresAtUtc);
    }

    // This overload makes unit tests deterministic.
    public StockReservation Reserve(
        Guid orderId,
        int quantity,
        DateTime reservedAtUtc,
        DateTime expiresAtUtc)
    {
        EnsureActive();
        EnsureUtc(reservedAtUtc, nameof(reservedAtUtc));
        EnsureUtc(expiresAtUtc, nameof(expiresAtUtc));

        if (orderId == Guid.Empty)
        {
            throw new DomainException(
                "OrderId cannot be empty.");
        }

        if (quantity <= 0)
        {
            throw new DomainException(
                "Reservation quantity must be greater than zero.");
        }

        var existingReservation = _reservations
            .SingleOrDefault(x => x.OrderId == orderId);

        if (existingReservation is not null)
        {
            if (!existingReservation.Matches(orderId, quantity))
            {
                throw new DomainException(
                    $"Order '{orderId}' already has a reservation " +
                    "with a different quantity.");
            }

            // Idempotent processing of the same request
            return existingReservation;
        }

        if (AvailableQuantity < quantity)
        {
            throw new DomainException(
                $"Insufficient stock for product '{ProductId}'. " +
                $"Requested: {quantity}, available: {AvailableQuantity}.");
        }

        var previousAvailableQuantity = AvailableQuantity;

        var reservation = StockReservation.Create(
            orderId,
            quantity,
            reservedAtUtc,
            expiresAtUtc);

        _reservations.Add(reservation);
        ReservedQuantity += quantity;

        RaiseEvent(new StockReservedDomainEvent(
            Id,
            ProductId,
            reservation.Id,
            orderId,
            quantity,
            reservedAtUtc,
            expiresAtUtc,
            AvailableQuantity));

        RaiseLowStockEventIfNeeded(previousAvailableQuantity);

        return reservation;
    }

    public void CommitReservation(
        Guid orderId,
        DateTime committedAtUtc)
    {
        EnsureUtc(committedAtUtc, nameof(committedAtUtc));

        var reservation = FindReservation(orderId);

        if (!reservation.Commit(committedAtUtc))
        {
            return;
        }

        OnHandQuantity -= reservation.Quantity;
        ReservedQuantity -= reservation.Quantity;

        RaiseEvent(new StockReservationCommittedDomainEvent(
            Id,
            ProductId,
            reservation.Id,
            orderId,
            reservation.Quantity,
            committedAtUtc,
            OnHandQuantity,
            ReservedQuantity,
            AvailableQuantity));
    }

    public void ReleaseReservation(
        Guid orderId,
        ReservationReleaseReason reason,
        DateTime releasedAtUtc)
    {
        EnsureUtc(releasedAtUtc, nameof(releasedAtUtc));

        var reservation = FindReservation(orderId);

        if (!reservation.Release(reason, releasedAtUtc))
        {
            return;
        }

        ReservedQuantity -= reservation.Quantity;

        RaiseEvent(new StockReservationReleasedDomainEvent(
            Id,
            ProductId,
            reservation.Id,
            orderId,
            reservation.Quantity,
            reason,
            releasedAtUtc,
            AvailableQuantity));
    }

    public void ExpireReservation(
        Guid orderId,
        DateTime expiredAtUtc)
    {
        EnsureUtc(expiredAtUtc, nameof(expiredAtUtc));

        var reservation = FindReservation(orderId);

        if (!reservation.Expire(expiredAtUtc))
        {
            return;
        }

        ReservedQuantity -= reservation.Quantity;

        RaiseEvent(new StockReservationExpiredDomainEvent(
            Id,
            ProductId,
            reservation.Id,
            orderId,
            reservation.Quantity,
            expiredAtUtc,
            AvailableQuantity));
    }

    public void ChangeReorderPoint(int reorderPoint)
    {
        if (reorderPoint < 0)
        {
            throw new DomainException(
                "Reorder point cannot be negative.");
        }

        if (ReorderPoint == reorderPoint)
        {
            return;
        }

        var previousReorderPoint = ReorderPoint;
        ReorderPoint = reorderPoint;

        RaiseEvent(new ReorderPointChangedDomainEvent(
            Id,
            ProductId,
            previousReorderPoint,
            reorderPoint));
    }

    public void Activate()
    {
        if (IsActive)
        {
            return;
        }

        IsActive = true;

        RaiseEvent(new InventoryItemActivatedDomainEvent(
            Id,
            ProductId));
    }

    public void Deactivate()
    {
        if (!IsActive)
        {
            return;
        }

        if (_reservations.Any(x => x.IsActive))
        {
            throw new DomainException(
                "Inventory item with active reservations cannot be deactivated.");
        }

        IsActive = false;

        RaiseEvent(new InventoryItemDeactivatedDomainEvent(
            Id,
            ProductId));
    }

    private StockReservation FindReservation(Guid orderId)
    {
        if (orderId == Guid.Empty)
        {
            throw new DomainException(
                "OrderId cannot be empty.");
        }

        return _reservations.SingleOrDefault(x => x.OrderId == orderId)
            ?? throw new DomainException(
                $"Reservation for order '{orderId}' was not found.");
    }

    private void EnsureActive()
    {
        if (!IsActive)
        {
            throw new DomainException(
                $"Inventory item '{Id}' is not active.");
        }
    }

    private static void EnsureReferenceId(Guid referenceId)
    {
        if (referenceId == Guid.Empty)
        {
            throw new DomainException(
                "ReferenceId cannot be empty.");
        }
    }

    private static void EnsureUtc(
        DateTime value,
        string parameterName)
    {
        if (value.Kind != DateTimeKind.Utc)
        {
            throw new DomainException(
                $"{parameterName} must use DateTimeKind.Utc.");
        }
    }

    private void RaiseLowStockEventIfNeeded(
        int previousAvailableQuantity)
    {
        var crossedReorderPoint =
            previousAvailableQuantity > ReorderPoint &&
            AvailableQuantity <= ReorderPoint;

        if (!crossedReorderPoint)
        {
            return;
        }

        RaiseEvent(new LowStockReachedDomainEvent(
            Id,
            ProductId,
            AvailableQuantity,
            ReorderPoint));
    }
}