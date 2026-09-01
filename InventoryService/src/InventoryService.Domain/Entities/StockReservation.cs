using Domain.Abstractions;
using InventoryService.Domain.Enums;
using InventoryService.Domain.Exceptions;

namespace InventoryService.Domain.Entities;

public sealed class StockReservation : Entity<Guid>
{
    // Required by EF Core
    private StockReservation()
    {
    }

    private StockReservation(
        Guid id,
        Guid orderId,
        int quantity,
        DateTime reservedAtUtc,
        DateTime expiresAtUtc)
        : base(id)
    {
        OrderId = orderId;
        Quantity = quantity;
        Status = StockReservationStatus.Reserved;
        ReservedAtUtc = reservedAtUtc;
        ExpiresAtUtc = expiresAtUtc;

        CreatedAt = reservedAtUtc;
        ModifiedAt = reservedAtUtc;
    }

    public Guid OrderId { get; private set; }

    public int Quantity { get; private set; }

    public StockReservationStatus Status { get; private set; }

    public DateTime ReservedAtUtc { get; private set; }

    public DateTime ExpiresAtUtc { get; private set; }

    public DateTime? FinalizedAtUtc { get; private set; }

    public ReservationReleaseReason? ReleaseReason { get; private set; }

    public bool IsActive =>
        Status == StockReservationStatus.Reserved;

    public bool IsFinalized =>
        Status is StockReservationStatus.Committed
            or StockReservationStatus.Released
            or StockReservationStatus.Expired;

    internal static StockReservation Create(
        Guid orderId,
        int quantity,
        DateTime reservedAtUtc,
        DateTime expiresAtUtc)
    {
        if (orderId == Guid.Empty)
        {
            throw new InvalidStockReservationException(
                "OrderId cannot be empty.");
        }

        if (quantity <= 0)
        {
            throw new InvalidStockReservationException(
                "Reservation quantity must be greater than zero.");
        }

        EnsureUtc(reservedAtUtc, nameof(reservedAtUtc));
        EnsureUtc(expiresAtUtc, nameof(expiresAtUtc));

        if (expiresAtUtc <= reservedAtUtc)
        {
            throw new InvalidStockReservationException(
                "Reservation expiration must be after reservation time.");
        }

        return new StockReservation(
            IdGenerator.New(),
            orderId,
            quantity,
            reservedAtUtc,
            expiresAtUtc);
    }

    /// <summary>
    /// Marks the reservation as committed.
    /// Returns false when the same operation was already completed.
    /// </summary>
    internal bool Commit(DateTime committedAtUtc)
    {
        EnsureUtc(committedAtUtc, nameof(committedAtUtc));

        // Idempotent retry
        if (Status == StockReservationStatus.Committed)
        {
            return false;
        }

        EnsureReserved(nameof(Commit));

        if (committedAtUtc >= ExpiresAtUtc)
        {
            throw new StockReservationExpiredException(
                Id,
                OrderId,
                ExpiresAtUtc);
        }

        Status = StockReservationStatus.Committed;
        FinalizedAtUtc = committedAtUtc;
        ReleaseReason = null;
        ModifiedAt = committedAtUtc;

        return true;
    }

    /// <summary>
    /// Releases stock because the order or payment has failed/cancelled.
    /// Returns false for an idempotent retry.
    /// </summary>
    internal bool Release(
        ReservationReleaseReason reason,
        DateTime releasedAtUtc)
    {
        EnsureUtc(releasedAtUtc, nameof(releasedAtUtc));

        if (reason == ReservationReleaseReason.None ||
            reason == ReservationReleaseReason.Expired)
        {
            throw new InvalidStockReservationException(
                $"Release reason '{reason}' is not valid for a manual release.");
        }

        // Idempotent retry of the same release
        if (Status == StockReservationStatus.Released &&
            ReleaseReason == reason)
        {
            return false;
        }

        EnsureReserved(nameof(Release));

        if (releasedAtUtc < ReservedAtUtc)
        {
            throw new InvalidStockReservationException(
                "Release time cannot be before reservation time.");
        }

        Status = StockReservationStatus.Released;
        ReleaseReason = reason;
        FinalizedAtUtc = releasedAtUtc;
        ModifiedAt = releasedAtUtc;

        return true;
    }

    /// <summary>
    /// Expires a reservation after its expiration time.
    /// Returns false for an idempotent retry.
    /// </summary>
    internal bool Expire(DateTime expiredAtUtc)
    {
        EnsureUtc(expiredAtUtc, nameof(expiredAtUtc));

        // Idempotent retry
        if (Status == StockReservationStatus.Expired)
        {
            return false;
        }

        EnsureReserved(nameof(Expire));

        if (expiredAtUtc < ExpiresAtUtc)
        {
            throw new InvalidReservationStateException(
                Id,
                Status,
                "Reservation cannot expire before ExpiresAtUtc.");
        }

        Status = StockReservationStatus.Expired;
        ReleaseReason = ReservationReleaseReason.Expired;
        FinalizedAtUtc = expiredAtUtc;
        ModifiedAt = expiredAtUtc;

        return true;
    }

    internal bool IsExpiredAt(DateTime utcNow)
    {
        EnsureUtc(utcNow, nameof(utcNow));

        return Status == StockReservationStatus.Reserved &&
               utcNow >= ExpiresAtUtc;
    }

    internal bool Matches(Guid orderId, int quantity)
    {
        return OrderId == orderId && Quantity == quantity;
    }

    private void EnsureReserved(string operation)
    {
        if (Status != StockReservationStatus.Reserved)
        {
            throw new InvalidReservationStateException(
                Id,
                Status,
                $"Cannot perform '{operation}' on a reservation with status '{Status}'.");
        }
    }

    private static void EnsureUtc(DateTime value, string parameterName)
    {
        if (value.Kind != DateTimeKind.Utc)
        {
            throw new InvalidStockReservationException(
                $"{parameterName} must use DateTimeKind.Utc.");
        }
    }
}