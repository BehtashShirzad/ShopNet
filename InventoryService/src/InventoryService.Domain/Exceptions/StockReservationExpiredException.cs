using Domain.Abstractions;

namespace InventoryService.Domain.Exceptions;

public sealed class StockReservationExpiredException : DomainException
{
    public StockReservationExpiredException(
        Guid reservationId,
        Guid orderId,
        DateTime expiresAtUtc)
        : base(
            $"Reservation '{reservationId}' for order '{orderId}' " +
            $"expired at '{expiresAtUtc:O}'.")
    {
        ReservationId = reservationId;
        OrderId = orderId;
        ExpiresAtUtc = expiresAtUtc;
    }

    public Guid ReservationId { get; }

    public Guid OrderId { get; }

    public DateTime ExpiresAtUtc { get; }
}