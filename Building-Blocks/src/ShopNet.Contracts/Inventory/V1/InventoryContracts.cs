namespace ShopNet.Contracts.Inventory.V1;

// Commands are sent to InventoryQueues.Commands, never broadcast as product/order events.
public static class InventoryQueues
{
    public const string Commands = "inventory-commands-v1";
    public const string CatalogProducts = "inventory-catalog-products-v1";
    public const string StockReceipts = "inventory-stock-receipts-v1";
}

public sealed record InventoryLine(Guid ProductId, int Quantity);
public sealed record ReserveInventory(Guid OrderId, Guid ReservationRequestId,
    InventoryLine[] Items, DateTimeOffset ExpiresAtUtc);
public sealed record CommitInventory(Guid OrderId, Guid ReservationRequestId);
public sealed record ReleaseInventory(Guid OrderId, Guid ReservationRequestId, InventoryReleaseReason Reason);
public enum InventoryReleaseReason { OrderCancelled = 1, PaymentFailed = 2, Compensation = 3 }

// Restricted to warehouse/admin publishers. ReferenceId identifies a single physical receipt.
public sealed record ReceiveInventoryStock(Guid ProductId, int Quantity, Guid ReferenceId);

public abstract record InventoryReservationEvent : IntegrationEvent
{
    // Monotonic within one attempt. Consumers ignore older versions and deduplicate EventId.
    public long ReservationVersion { get; init; }
}

public sealed record InventoryReserved(Guid OrderId, Guid ReservationRequestId,
    InventoryLine[] Items, DateTimeOffset ExpiresAtUtc) : InventoryReservationEvent;
public sealed record InventoryRejected(Guid OrderId, Guid ReservationRequestId,
    string Reason) : InventoryReservationEvent;
public sealed record InventoryCommitted(Guid OrderId, Guid ReservationRequestId) : InventoryReservationEvent;
public sealed record InventoryReleased(Guid OrderId, Guid ReservationRequestId) : InventoryReservationEvent;
public sealed record InventoryExpired(Guid OrderId, Guid ReservationRequestId) : InventoryReservationEvent;
public sealed record InventoryCommandRejected(Guid OrderId, Guid ReservationRequestId,
    string Operation, string Reason) : IntegrationEvent;
