using InventoryService.Application;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace InventoryService.Infrastructure;

public sealed class ReservationExpiryWorker(IServiceScopeFactory scopes, TimeProvider clock,
    ILogger<ReservationExpiryWorker> logger) : BackgroundService
{
    public async Task RunOnceAsync(CancellationToken ct)
    {
        IReadOnlyList<ExpiredAttempt> expired;
        await using (var scope = scopes.CreateAsyncScope())
            expired = await scope.ServiceProvider.GetRequiredService<IInventoryStore>()
                .GetExpiredAsync(clock.GetUtcNow(), 100, ct);
        foreach (var attempt in expired)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<InventoryOperations>()
                    .ExpireAsync(attempt.OrderId, attempt.ReservationRequestId, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception exception)
            {
                logger.LogError(exception, "Failed to expire inventory attempt {RequestId}; next scan will retry.",
                    attempt.ReservationRequestId);
            }
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5), clock);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try { await RunOnceAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception)
            {
                logger.LogError(exception, "Inventory expiry scan failed; next scan will retry.");
            }
        }
    }
}
