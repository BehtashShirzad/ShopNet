using System.Security.Cryptography;
using System.Text;
using Domain.Abstractions;
using InventoryService.Domain.Aggregates;
using InventoryService.Domain.Enums;
using ShopNet.Contracts.Inventory.V1;

namespace InventoryService.Application;

public sealed class InventoryOperations(IInventoryStore store, IInventoryEventPublisher events, TimeProvider clock)
{
    public const int MaxBatchSize = 100;

    public Task RegisterProductAsync(Guid productId, CancellationToken ct = default)
    {
        RequireId(productId, nameof(productId));
        return store.ExecuteAsync(ProductKey(productId), async () =>
        {
            if (await store.FindProductAsync(productId, ct) is null)
                store.Add(InventoryItem.Create(productId));
        }, ct);
    }

    public Task ReceiveStockAsync(ReceiveInventoryStock command, CancellationToken ct = default)
    {
        RequireId(command.ProductId, nameof(command.ProductId));
        RequireId(command.ReferenceId, nameof(command.ReferenceId));
        if (command.Quantity <= 0) throw new ArgumentException("Quantity must be positive.");
        return store.ExecuteAsync($"receipt:{command.ReferenceId:N}", async () =>
        {
            var receipt = await store.FindReceiptAsync(command.ReferenceId, ct);
            if (receipt is not null)
            {
                if (receipt.ProductId != command.ProductId || receipt.Quantity != command.Quantity)
                    throw new ArgumentException("ReferenceId was reused with a different receipt.");
                return;
            }
            await store.LockAsync(ProductKey(command.ProductId), ct);
            var item = await store.FindProductAsync(command.ProductId, ct)
                ?? throw new DomainException("Product is not registered in Inventory.");
            item.ReceiveStock(command.Quantity, command.ReferenceId);
            store.Add(new StockReceipt(command.ReferenceId, command.ProductId, command.Quantity));
        }, ct);
    }

    public Task ReserveAsync(ReserveInventory command, CancellationToken ct = default)
    {
        RequireId(command.OrderId, nameof(command.OrderId));
        RequireId(command.ReservationRequestId, nameof(command.ReservationRequestId));
        var items = ValidateItems(command.Items);
        var fingerprint = Fingerprint(items, command.ExpiresAtUtc);
        return store.ExecuteAsync(RequestKey(command.ReservationRequestId), async () =>
        {
            await store.LockAsync(OrderKey(command.OrderId), ct);
            var existing = await store.FindAttemptAsync(command.ReservationRequestId, ct);
            if (existing is not null)
            {
                if (existing.OrderId != command.OrderId ||
                    (existing.Fingerprint.Length > 0 && existing.Fingerprint != fingerprint))
                {
                    await RejectCommand(command.OrderId, command.ReservationRequestId, "Reserve",
                        "RequestIdConflict", ct);
                    return;
                }
                // Replays report the CURRENT result, never resurrect an old reservation.
                if (existing.Status == AttemptStatus.Reserved && existing.ExpiresAtUtc <= clock.GetUtcNow())
                    await ExpireLoaded(existing, ct);
                else
                    await events.PublishAsync(existing.ToEvent(), ct);
                return;
            }

            var now = clock.GetUtcNow();
            string? reason = command.ExpiresAtUtc <= now ? "DeadlineElapsed" : null;
            if (reason is null && command.ExpiresAtUtc > now.AddHours(24))
                reason = "DeadlineTooFar";
            if (reason is null && await store.HasBlockingAttemptAsync(command.OrderId, ct))
                reason = "OrderAlreadyReservedOrCommitted";

            var products = new List<InventoryItem>();
            if (reason is null)
            {
                foreach (var line in items)
                {
                    await store.LockAsync(ProductKey(line.ProductId), ct);
                    var product = await store.FindProductAsync(line.ProductId, ct);
                    if (product is null) { reason = "ProductNotFound"; break; }
                    if (!product.IsActive) { reason = "ProductInactive"; break; }
                    if (product.AvailableQuantity < line.Quantity) { reason = "InsufficientStock"; break; }
                    products.Add(product);
                }
            }

            // No product is mutated until every line has passed validation under database locks.
            now = clock.GetUtcNow();
            if (reason is null && command.ExpiresAtUtc <= now) reason = "DeadlineElapsed";
            if (reason is null)
                foreach (var product in products)
                    product.Reserve(command.OrderId, command.ReservationRequestId,
                        items.Single(x => x.ProductId == product.ProductId).Quantity,
                        now.UtcDateTime, command.ExpiresAtUtc.UtcDateTime);

            var attempt = ReservationAttempt.Create(command.OrderId, command.ReservationRequestId,
                fingerprint, items, command.ExpiresAtUtc.ToUniversalTime(),
                reason is null ? AttemptStatus.Reserved : AttemptStatus.Rejected, reason ?? "", now);
            store.Add(attempt);
            await events.PublishAsync(attempt.ToEvent(), ct);
        }, ct);
    }

    public Task CommitAsync(CommitInventory command, CancellationToken ct = default)
        => FinalizeAsync(command.OrderId, command.ReservationRequestId, null, false, ct);

    public Task ReleaseAsync(ReleaseInventory command, CancellationToken ct = default)
    {
        if (!Enum.IsDefined(command.Reason)) throw new ArgumentException("Invalid release reason.");
        return FinalizeAsync(command.OrderId, command.ReservationRequestId, command.Reason, false, ct);
    }

    public Task ExpireAsync(Guid orderId, Guid requestId, CancellationToken ct = default)
        => FinalizeAsync(orderId, requestId, null, true, ct);

    private Task FinalizeAsync(Guid orderId, Guid requestId, InventoryReleaseReason? releaseReason,
        bool expire, CancellationToken ct)
    {
        RequireId(orderId, nameof(orderId));
        RequireId(requestId, nameof(requestId));
        return store.ExecuteAsync(RequestKey(requestId), async () =>
        {
            await store.LockAsync(OrderKey(orderId), ct);
            var attempt = await store.FindAttemptAsync(requestId, ct);
            var operation = expire ? "Expire" : releaseReason.HasValue ? "Release" : "Commit";
            var now = clock.GetUtcNow();
            if (attempt is null)
            {
                if (releaseReason.HasValue)
                {
                    // Cancellation can overtake reservation. Persist a tombstone to fence delayed Reserve.
                    attempt = ReservationAttempt.Create(orderId, requestId, "", [], now,
                        AttemptStatus.Released, releaseReason.ToString()!, now);
                    store.Add(attempt);
                    await events.PublishAsync(attempt.ToEvent(), ct);
                }
                else if (!expire)
                    await RejectCommand(orderId, requestId, operation, "ReservationNotFound", ct);
                return;
            }
            if (attempt.OrderId != orderId)
            {
                await RejectCommand(orderId, requestId, operation, "RequestIdConflict", ct);
                return;
            }
            if (attempt.Status != AttemptStatus.Reserved)
            {
                if (expire) return;
                // Return current terminal state; the caller must not treat it as a successful commit.
                await events.PublishAsync(attempt.ToEvent(), ct);
                return;
            }
            if (attempt.ExpiresAtUtc <= now)
            {
                await ExpireLoaded(attempt, ct);
                return;
            }
            if (expire) return;
            var products = await LoadAttemptProducts(attempt, ct);
            now = clock.GetUtcNow();
            if (attempt.ExpiresAtUtc <= now)
            {
                await ExpireLoaded(attempt, ct);
                return;
            }
            foreach (var product in products)
            {
                if (releaseReason.HasValue)
                    product.ReleaseReservation(orderId, requestId, releaseReason.Value switch
                    {
                        InventoryReleaseReason.OrderCancelled => ReservationReleaseReason.OrderCancelled,
                        InventoryReleaseReason.PaymentFailed => ReservationReleaseReason.PaymentFailed,
                        _ => ReservationReleaseReason.InventoryCompensation
                    }, now.UtcDateTime);
                else product.CommitReservation(orderId, requestId, now.UtcDateTime);
            }
            attempt.Transition(releaseReason.HasValue ? AttemptStatus.Released : AttemptStatus.Committed,
                releaseReason?.ToString() ?? "", now);
            await events.PublishAsync(attempt.ToEvent(), ct);
        }, ct);
    }

    private async Task ExpireLoaded(ReservationAttempt attempt, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        foreach (var product in await LoadAttemptProducts(attempt, ct))
            product.ExpireReservation(attempt.OrderId, attempt.Id, now.UtcDateTime);
        attempt.Transition(AttemptStatus.Expired, "DeadlineElapsed", now);
        await events.PublishAsync(attempt.ToEvent(), ct);
    }

    private async Task<List<InventoryItem>> LoadAttemptProducts(ReservationAttempt attempt, CancellationToken ct)
    {
        var products = new List<InventoryItem>();
        foreach (var line in attempt.Items.OrderBy(x => x.ProductId))
        {
            await store.LockAsync(ProductKey(line.ProductId), ct);
            products.Add(await store.FindProductAsync(line.ProductId, ct)
                ?? throw new InvalidOperationException("A reserved inventory item is missing."));
        }
        return products;
    }

    private Task RejectCommand(Guid orderId, Guid requestId, string operation, string reason, CancellationToken ct)
        => events.PublishAsync(new InventoryCommandRejected(orderId, requestId, operation, reason)
            { OccurredOnUtc = clock.GetUtcNow() }, ct);

    public static InventoryLine[] ValidateItems(InventoryLine[]? items)
    {
        if (items is null || items.Length is 0 or > MaxBatchSize ||
            items.Any(x => x is null || x.ProductId == Guid.Empty || x.Quantity <= 0) ||
            items.Select(x => x.ProductId).Distinct().Count() != items.Length)
            throw new ArgumentException("Provide 1-100 distinct products with positive quantities.");
        return items.OrderBy(x => x.ProductId).ToArray();
    }

    private static string Fingerprint(InventoryLine[] items, DateTimeOffset expiresAtUtc)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            expiresAtUtc.UtcTicks + "|" + string.Join("|", items.Select(x => $"{x.ProductId:N}:{x.Quantity}")))));

    private static void RequireId(Guid id, string name)
    {
        if (id == Guid.Empty) throw new ArgumentException($"{name} cannot be empty.");
    }
    private static string ProductKey(Guid id) => $"product:{id:N}";
    private static string RequestKey(Guid id) => $"request:{id:N}";
    private static string OrderKey(Guid id) => $"order:{id:N}";
}
