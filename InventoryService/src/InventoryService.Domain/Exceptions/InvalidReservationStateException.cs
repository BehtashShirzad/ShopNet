using Domain.Abstractions;
using InventoryService.Domain.Enums;

namespace InventoryService.Domain.Exceptions;

public sealed class InvalidReservationStateException : DomainException
{
    public InvalidReservationStateException(
        Guid reservationId,
        StockReservationStatus currentStatus,
        string message)
        : base(
            $"Reservation '{reservationId}' is in status " +
            $"'{currentStatus}'. {message}")
    {
        ReservationId = reservationId;
        CurrentStatus = currentStatus;
    }

    public Guid ReservationId { get; }

    public StockReservationStatus CurrentStatus { get; }
}