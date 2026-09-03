using System.Threading.Channels;
using CartService.Infrastructure;
using MassTransit;
using MassTransit.EntityFrameworkCoreIntegration;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OrderService.Domain.Aggregates;
using OrderService.Domain.Enums;
using OrderService.Infrastructure;
using ShopNet.Contracts.IntegrationEvents;

namespace CartService.IntegrationTests;

[Collection(CartContainersCollection.Name)]
public sealed class CheckoutMessagingTests(CartContainersFixture fixture)
{
    [Fact]
    public async Task PausedCheckoutSurvivesApplicationRestartAndRealOrderDeduplicatesReplay()
    {
        await using var order = await CheckoutOrderRuntime.Create(fixture);
        string prefix;
        CartCheckedOutEvent message;
        await using (var paused = await CheckoutTestHost.Create(fixture))
        {
            var cart = await paused.Seed();
            await paused.Checkout(cart.Id);
            var saved = await paused.Read(cart.Id);
            prefix = paused.Keys.KeyPrefix;
            var payload = await paused.Redis.HashGetAsync(paused.Keys.MessagesKey, saved.CheckoutEventId!.Value.ToString("N"));
            Assert.False(payload.IsNull);
            message = JsonConvert.DeserializeObject<CartCheckedOutEvent>(payload.ToString())!;
            Assert.Equal(1, await paused.PendingCount());
            Assert.Null(await order.ByCart(cart.Id));
        }
        // Same Redis namespace, a new Cart process scope, with the hosted dispatcher now enabled.
        await using var resumed = await CheckoutTestHost.Create(fixture, prefix, delivery: true);
        var first = await order.Consumed.Next();
        Assert.Equal(message.EventId, first.EventId);
        Assert.Equal(message.EventId, first.MessageId);
        Assert.Equal(message.CartId, first.CorrelationId);
        await Until(async () => await resumed.PendingCount() == 0);
        var savedOrder = (await order.ByCart(message.CartId))!;
        Assert.Equal(message.CustomerId, savedOrder.CustomerId);
        Assert.Equal(message.TotalPrice, savedOrder.TotalPrice);
        Assert.Equal(OrderInventoryStatus.Requested, savedOrder.InventoryStatus);
        Assert.NotNull(savedOrder.InventoryReservationRequestId);
        Assert.Equal(2, await order.OutboxCount()); // OrderCreated plus ReserveInventory; delivery intentionally paused.

        await resumed.Services.GetRequiredService<ICheckoutPublisher>().PublishAsync(message, default);
        await order.Consumed.Next(); // Wait for actual Order consumer completion, not an arbitrary sleep.
        Assert.Equal(savedOrder.Id, (await order.ByCart(message.CartId))!.Id);
        Assert.Equal(savedOrder.InventoryReservationRequestId, (await order.ByCart(message.CartId))!.InventoryReservationRequestId);
        Assert.Equal(2, await order.OutboxCount());
        resumed.Upstream.CatalogFailure = "unavailable";
        resumed.Upstream.InventoryFailure = "unavailable";
        Assert.Equal(message.CartId, await resumed.Checkout(message.CartId));
        Assert.Equal(message.EventId, (await resumed.Read(message.CartId)).CheckoutEventId);
        Assert.Equal(0, resumed.Upstream.InventoryCalls);
        Assert.Equal(0, await resumed.PendingCount());
    }

    [Fact]
    public async Task CrashAfterRabbitConfirmationBeforeRedisAckReplaysSameIdentity()
    {
        await using var order = await CheckoutOrderRuntime.Create(fixture);
        await using var host = await CheckoutTestHost.Create(fixture);
        var cart = await host.Seed();
        await host.Checkout(cart.Id);
        var lease = (await host.Outbox.ClaimAsync(default))!;
        var message = JsonConvert.DeserializeObject<CartCheckedOutEvent>(lease.Payload)!;
        await host.Services.GetRequiredService<ICheckoutPublisher>().PublishAsync(message, default);
        var first = await order.Consumed.Next();
        Assert.Equal(1, await host.PendingCount()); // Simulated crash: deliberately no ACK.
        await host.Redis.SortedSetAddAsync(host.Keys.PendingKey, lease.EventId.ToString("N"), 0);
        Assert.Equal(1, await host.Services.GetRequiredService<CartOutboxDispatcher>().RunOnceAsync(default));
        var replay = await order.Consumed.Next();
        Assert.Equal(first, replay);
        Assert.Equal(message.EventId, replay.MessageId);
        Assert.False(await host.Outbox.AcknowledgeAsync(lease, default));
        Assert.Equal(0, await host.PendingCount());
        Assert.Equal(2, await order.OutboxCount());
        Assert.Equal(message.TotalPrice, (await order.ByCart(cart.Id))!.TotalPrice);
    }

    private static async Task Until(Func<Task<bool>> predicate)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        while (!await predicate()) await Task.Delay(100, timeout.Token);
    }
}

internal sealed class CheckoutOrderRuntime : IAsyncDisposable
{
    private readonly IHost _host;
    private readonly ConnectHandle _observer;
    public CheckoutConsumeObserver Consumed { get; }
    private CheckoutOrderRuntime(IHost host, CheckoutConsumeObserver observer, ConnectHandle connection)
        => (_host, Consumed, _observer) = (host, observer, connection);

    public static async Task<CheckoutOrderRuntime> Create(CartContainersFixture fixture)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { EnvironmentName = "Testing" });
        builder.Logging.SetMinimumLevel(LogLevel.Error);
        var settings = CheckoutTestHost.RabbitSettings(fixture.RabbitMqConnectionString);
        settings["ConnectionStrings:OrderServiceConnection"] = new SqlConnectionStringBuilder(fixture.SqlConnectionString)
            { InitialCatalog = $"CartOrder_{Guid.NewGuid():N}" }.ConnectionString;
        settings["Order:QueuePrefix"] = $"cart-order-{Guid.NewGuid():N}-";
        settings["OrderOutbox:DeliveryEnabled"] = "false";
        builder.Configuration.AddInMemoryCollection(settings);
        OrderService.Application.DependencyInjection.AddApplicationServices(builder.Services);
        builder.Services.AddInfrastructureServices(builder.Configuration);
        var host = builder.Build();
        try
        {
            await using (var scope = host.Services.CreateAsyncScope())
                await scope.ServiceProvider.GetRequiredService<WriteDbContext>().Database.MigrateAsync();
            LogContext.ConfigureCurrentLogContext(host.Services.GetRequiredService<ILoggerFactory>());
            var observer = new CheckoutConsumeObserver();
            var handle = host.Services.GetRequiredService<IBus>().ConnectConsumeObserver(observer);
            await host.StartAsync();
            return new(host, observer, handle);
        }
        catch { host.Dispose(); throw; }
    }
    public async Task<OrderAggregate?> ByCart(Guid id)
    {
        await using var scope = _host.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<WriteDbContext>().Orders
            .AsNoTracking().Include(x => x.Items).SingleOrDefaultAsync(x => x.CartId == id);
    }
    public async Task<int> OutboxCount()
    {
        await using var scope = _host.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<WriteDbContext>().Set<OutboxMessage>().CountAsync();
    }
    public async ValueTask DisposeAsync()
    {
        _observer.Disconnect();
        try { await _host.StopAsync(); }
        finally { _host.Dispose(); }
    }
}

internal sealed class CheckoutConsumeObserver : IConsumeObserver
{
    private readonly Channel<(Guid EventId, Guid? MessageId, Guid? CorrelationId)> _completed =
        Channel.CreateUnbounded<(Guid, Guid?, Guid?)>();
    public Task PreConsume<T>(ConsumeContext<T> context) where T : class => Task.CompletedTask;
    public Task ConsumeFault<T>(ConsumeContext<T> context, Exception exception) where T : class
    {
        _completed.Writer.TryComplete(exception);
        return Task.CompletedTask;
    }
    public Task PostConsume<T>(ConsumeContext<T> context) where T : class
    {
        if (context.Message is CartCheckedOutEvent message)
            _completed.Writer.TryWrite((message.EventId, context.MessageId, context.CorrelationId));
        return Task.CompletedTask;
    }
    public async Task<(Guid EventId, Guid? MessageId, Guid? CorrelationId)> Next()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        return await _completed.Reader.ReadAsync(timeout.Token);
    }
}
