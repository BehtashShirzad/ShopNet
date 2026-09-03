using ShopNet.Contracts.Inventory.V1;

namespace OrderService.Application.Inventory;

public interface IInventoryCommandSender
{
    Task ReserveAsync(ReserveInventory command, CancellationToken cancellationToken);
}

public interface IOrderTransactionLock
{
    Task AcquireAsync(string resource, CancellationToken cancellationToken);
}

public sealed class OrderInventoryOptions
{
    public TimeSpan ReservationDuration { get; init; } = TimeSpan.FromMinutes(15);
    public void Validate()
    {
        if (ReservationDuration <= TimeSpan.Zero || ReservationDuration > TimeSpan.FromHours(24))
            throw new ArgumentException("Inventory reservation duration must be greater than zero and no more than 24 hours.");
    }
}
