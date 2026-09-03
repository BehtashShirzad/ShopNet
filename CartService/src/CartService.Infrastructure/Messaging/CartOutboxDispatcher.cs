using MassTransit;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using ShopNet.Contracts.IntegrationEvents;

namespace CartService.Infrastructure;

public interface ICheckoutPublisher
{
    Task PublishAsync(CartCheckedOutEvent message, CancellationToken cancellationToken);
}
public sealed class CheckoutPublisher(IBus bus) : ICheckoutPublisher
{
    public Task PublishAsync(CartCheckedOutEvent message, CancellationToken cancellationToken)
        => bus.Publish(message, context =>
        {
            context.MessageId = message.EventId;
            context.CorrelationId = message.CartId;
        }, cancellationToken);
}

// Publication happens only after the Redis atomic checkout has succeeded.
public sealed class CartOutboxDispatcher(ICheckoutOutbox outbox, ICheckoutPublisher publisher,
    ILogger<CartOutboxDispatcher> logger) : BackgroundService
{
    public async Task<int> RunOnceAsync(CancellationToken ct)
    {
        var delivered = 0;
        for (var index = 0; index < 20; index++)
        {
            var lease = await outbox.ClaimAsync(ct);
            if (lease is null) break;
            try
            {
                var message = JsonConvert.DeserializeObject<CartCheckedOutEvent>(lease.Payload)
                    ?? throw new InvalidOperationException("Invalid checkout payload.");
                if (message.EventId != lease.EventId) throw new InvalidOperationException("Checkout event identity mismatch.");
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeout.CancelAfter(TimeSpan.FromSeconds(10));
                await publisher.PublishAsync(message, timeout.Token);
                if (await outbox.AcknowledgeAsync(lease, ct)) delivered++;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception exception)
            {
                logger.LogError(exception, "Checkout delivery failed for {EventId}; keeping the outbox entry.", lease.EventId);
                await outbox.RetryAsync(lease, ct);
            }
        }
        return delivered;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try { await RunOnceAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception) { logger.LogError(exception, "Cart outbox scan failed; will retry."); }
        }
    }
}
