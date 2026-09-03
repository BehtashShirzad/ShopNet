using System.Data;
using InventoryService.Application;
using InventoryService.Domain.Aggregates;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace InventoryService.Infrastructure;

public sealed class SqlInventoryStore(InventoryDbContext db) : IInventoryStore
{
    public async Task ExecuteAsync(string lockKey, Func<Task> action, CancellationToken cancellationToken)
    {
        if (db.Database.CurrentTransaction is not null)
            throw new InvalidOperationException("Nested inventory transactions are not supported.");
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        await LockAsync(lockKey, cancellationToken);
        await action();
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        foreach (var item in db.ChangeTracker.Entries<InventoryItem>())
            item.Entity.ClearEvents();
        // The caller disposes this scope even on failure; never retry a failed tracked context.
    }

    public async Task LockAsync(string key, CancellationToken cancellationToken)
    {
        if (db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Inventory locks require an active transaction.");
        var parameter = new SqlParameter("@resource", SqlDbType.NVarChar, 255)
            { Value = "inventory:" + key };
        await db.Database.ExecuteSqlRawAsync("""
            DECLARE @result int;
            EXEC @result = sys.sp_getapplock @Resource = @resource,
                @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 10000;
            IF @result < 0 THROW 51000, 'Could not acquire inventory transaction lock.', 1;
            """, [parameter], cancellationToken);
    }

    public Task<InventoryItem?> FindProductAsync(Guid productId, CancellationToken ct)
        => db.InventoryItems.Include(x => x.Reservations).SingleOrDefaultAsync(x => x.ProductId == productId, ct);
    public void Add(InventoryItem item) => db.InventoryItems.Add(item);
    public Task<ReservationAttempt?> FindAttemptAsync(Guid requestId, CancellationToken ct)
        => db.ReservationAttempts.SingleOrDefaultAsync(x => x.Id == requestId, ct);
    public Task<bool> HasBlockingAttemptAsync(Guid orderId, CancellationToken ct)
        => db.ReservationAttempts.AnyAsync(x => x.OrderId == orderId &&
            (x.Status == AttemptStatus.Reserved || x.Status == AttemptStatus.Committed), ct);
    public void Add(ReservationAttempt attempt) => db.ReservationAttempts.Add(attempt);
    public Task<StockReceipt?> FindReceiptAsync(Guid referenceId, CancellationToken ct)
        => db.StockReceipts.SingleOrDefaultAsync(x => x.ReferenceId == referenceId, ct);
    public void Add(StockReceipt receipt) => db.StockReceipts.Add(receipt);

    public async Task<IReadOnlyList<ExpiredAttempt>> GetExpiredAsync(DateTimeOffset now, int limit, CancellationToken ct)
        => await db.ReservationAttempts.AsNoTracking()
            .Where(x => x.Status == AttemptStatus.Reserved && x.ExpiresAtUtc <= now)
            .OrderBy(x => x.ExpiresAtUtc).Take(limit)
            .Select(x => new ExpiredAttempt(x.OrderId, x.Id)).ToListAsync(ct);

    public async Task<IReadOnlyList<InventoryAvailability>> GetAvailabilityAsync(Guid[] productIds, CancellationToken ct)
    {
        if (productIds.Length is 0 or > InventoryOperations.MaxBatchSize ||
            productIds.Any(x => x == Guid.Empty))
            throw new ArgumentException("Provide 1-100 non-empty product IDs.");
        var products = await db.InventoryItems.AsNoTracking().Where(x => productIds.Contains(x.ProductId))
            .Select(x => new InventoryAvailability(x.ProductId, true, x.IsActive,
                x.IsActive ? x.OnHandQuantity - x.ReservedQuantity : 0)).ToListAsync(ct);
        var found = products.ToDictionary(x => x.ProductId);
        return productIds.Distinct().Select(id => found.GetValueOrDefault(id)
            ?? new InventoryAvailability(id, false, false, 0)).ToArray();
    }
}
