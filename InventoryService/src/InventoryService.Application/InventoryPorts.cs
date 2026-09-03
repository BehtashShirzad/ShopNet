using InventoryService.Domain.Aggregates;
using ShopNet.Contracts;

namespace InventoryService.Application;

public interface IInventoryStore
{
    // A fresh scope per operation/retry. Commit includes domain state, receipts and outgoing messages.
    Task ExecuteAsync(string lockKey, Func<Task> action, CancellationToken cancellationToken);
    Task LockAsync(string key, CancellationToken cancellationToken);
    Task<InventoryItem?> FindProductAsync(Guid productId, CancellationToken cancellationToken);
    void Add(InventoryItem item);
    Task<ReservationAttempt?> FindAttemptAsync(Guid requestId, CancellationToken cancellationToken);
    Task<bool> HasBlockingAttemptAsync(Guid orderId, CancellationToken cancellationToken);
    void Add(ReservationAttempt attempt);
    Task<StockReceipt?> FindReceiptAsync(Guid referenceId, CancellationToken cancellationToken);
    void Add(StockReceipt receipt);
    Task<IReadOnlyList<ExpiredAttempt>> GetExpiredAsync(DateTimeOffset now, int limit, CancellationToken cancellationToken);
    Task<IReadOnlyList<InventoryAvailability>> GetAvailabilityAsync(Guid[] productIds, CancellationToken cancellationToken);
}

public interface IInventoryEventPublisher
{
    Task PublishAsync(IntegrationEvent message, CancellationToken cancellationToken);
}

public sealed record StockReceipt(Guid ReferenceId, Guid ProductId, int Quantity);
public sealed record ExpiredAttempt(Guid OrderId, Guid ReservationRequestId);
public sealed record InventoryAvailability(Guid ProductId, bool Exists, bool IsActive, int AvailableQuantity);
