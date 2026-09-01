using Domain.Abstractions;

namespace InventoryService.Domain.Exceptions;

public sealed class InvalidStockReservationException : DomainException
{
    public InvalidStockReservationException(string message)
        : base(message)
    {
    }
}