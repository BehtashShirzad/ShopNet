using System.Text.Json;
using ShopNet.Contracts;
using ShopNet.Contracts.Inventory.V1;

namespace InventoryService.Application;

public enum AttemptStatus { Reserved = 1, Rejected = 2, Committed = 3, Released = 4, Expired = 5 }

// Durable business idempotency record spanning every product in one reservation attempt.
// Kept after completion; it is not the future Order saga.
public sealed class ReservationAttempt
{
    private ReservationAttempt() { }
    public Guid Id { get; private set; }
    public Guid OrderId { get; private set; }
    public string Fingerprint { get; private set; } = "";
    public string ItemsJson { get; private set; } = "[]";
    public DateTimeOffset ExpiresAtUtc { get; private set; }
    public AttemptStatus Status { get; private set; }
    public string Reason { get; private set; } = "";
    public Guid EventId { get; private set; }
    public long Version { get; private set; }
    public DateTimeOffset OccurredOnUtc { get; private set; }
    public InventoryLine[] Items => JsonSerializer.Deserialize<InventoryLine[]>(ItemsJson)!;

    public static ReservationAttempt Create(Guid orderId, Guid requestId, string fingerprint,
        InventoryLine[] items, DateTimeOffset expiresAtUtc, AttemptStatus status, string reason,
        DateTimeOffset now)
    {
        var attempt = new ReservationAttempt { Id = requestId, OrderId = orderId,
            Fingerprint = fingerprint, ItemsJson = JsonSerializer.Serialize(items), ExpiresAtUtc = expiresAtUtc };
        attempt.Transition(status, reason, now);
        return attempt;
    }

    public void Transition(AttemptStatus status, string reason, DateTimeOffset now)
    {
        Status = status;
        Version++;
        Reason = reason;
        EventId = Guid.NewGuid();
        OccurredOnUtc = now;
    }

    public IntegrationEvent ToEvent()
    {
        InventoryReservationEvent result = Status switch
        {
            AttemptStatus.Reserved => new InventoryReserved(OrderId, Id, Items, ExpiresAtUtc),
            AttemptStatus.Rejected => new InventoryRejected(OrderId, Id, Reason),
            AttemptStatus.Committed => new InventoryCommitted(OrderId, Id),
            AttemptStatus.Released => new InventoryReleased(OrderId, Id),
            AttemptStatus.Expired => new InventoryExpired(OrderId, Id),
            _ => throw new InvalidOperationException("Unknown reservation attempt state.")
        };
        return result with { EventId = EventId, OccurredOnUtc = OccurredOnUtc, ReservationVersion = Version };
    }
}
