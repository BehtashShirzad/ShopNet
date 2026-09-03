using System.Threading.Channels;
using Application.Abstractions.Contracts;
using InventoryService.Application;
using InventoryService.Infrastructure;
using MassTransit;
using MassTransit.EntityFrameworkCoreIntegration;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OrderService.Api;
using OrderService.Application;
using OrderService.Domain.Aggregates;
using OrderService.Infrastructure;
using ShopNet.Contracts.Inventory.V1;

namespace OrderService.IntegrationTests;

internal sealed class OrderTestClock : TimeProvider
{
    public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;
    public override DateTimeOffset GetUtcNow() => Now;
}

internal sealed class OrderInventoryTestHost : IAsyncDisposable
{
    public WebApplication App { get; }
    public IServiceProvider Services => App.Services;
    public string ConnectionString { get; }
    public string Prefix { get; }
    public string CommandQueue { get; }
    public OrderTestClock Clock { get; }
    public IBus Bus => Services.GetRequiredService<IBus>();

    private OrderInventoryTestHost(WebApplication app, string connection, string prefix, string queue, OrderTestClock clock)
        => (App, ConnectionString, Prefix, CommandQueue, Clock) = (app, connection, prefix, queue, clock);

    public static Dictionary<string, string?> RabbitSettings(string connection)
    {
        var rabbit = new Uri(connection);
        var credentials = rabbit.UserInfo.Split(':', 2);
        return new()
        {
            ["RabbitMq:Host"] = rabbit.Host, ["RabbitMq:Port"] = rabbit.Port.ToString(),
            ["RabbitMq:VirtualHost"] = "/",
            ["RabbitMq:Username"] = Uri.UnescapeDataString(credentials[0]),
            ["RabbitMq:Password"] = Uri.UnescapeDataString(credentials[1])
        };
    }

    public static async Task<OrderInventoryTestHost> Create(OrderContainersFixture fixture,
        bool delivery = false, string? connection = null, string? prefix = null, string? commandQueue = null,
        OrderTestClock? clock = null, bool migrate = true)
    {
        connection ??= new SqlConnectionStringBuilder(fixture.DatabaseConnectionString)
            { InitialCatalog = $"OrderRuntime_{Guid.NewGuid():N}" }.ConnectionString;
        prefix ??= $"order-test-{Guid.NewGuid():N}-";
        commandQueue ??= prefix + InventoryQueues.Commands;
        clock ??= new OrderTestClock();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
        builder.WebHost.UseTestServer();
        builder.Logging.SetMinimumLevel(LogLevel.Error);
        var settings = RabbitSettings(fixture.RabbitMqConnectionString);
        settings["ConnectionStrings:OrderServiceConnection"] = connection;
        settings["Order:QueuePrefix"] = prefix;
        settings["OrderOutbox:DeliveryEnabled"] = delivery.ToString();
        settings["Inventory:CommandQueue"] = commandQueue;
        settings["Inventory:ReservationMinutes"] = "15";
        builder.Configuration.AddInMemoryCollection(settings);
        builder.Services.AddSingleton<TimeProvider>(clock);
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddApplicationServices();
        builder.Services.AddInfrastructureServices(builder.Configuration);
        var app = builder.Build();
        app.MapEndpoint();
        try
        {
            if (migrate)
            {
                await using var scope = app.Services.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<WriteDbContext>().Database.MigrateAsync();
            }
            return new(app, connection, prefix, commandQueue, clock);
        }
        catch { await app.DisposeAsync(); throw; }
    }

    public async Task Start()
    {
        // These tests restart independent hosts in one process. Do not inherit a disposed host's AsyncLocal log context.
        LogContext.ConfigureCurrentLogContext(Services.GetRequiredService<ILoggerFactory>());
        await App.StartAsync();
    }

    public async Task Send<T>(T command) where T : IRequest
    {
        await using var scope = Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ISender>().Send(command);
    }

    public async Task<OrderAggregate?> ByCart(Guid cartId)
    {
        await using var scope = Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<WriteDbContext>()
            .Orders.AsNoTracking().SingleOrDefaultAsync(x => x.CartId == cartId);
    }

    public async Task<List<OutboxMessage>> Outbox()
    {
        await using var scope = Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<WriteDbContext>().Set<OutboxMessage>().AsNoTracking().ToListAsync();
    }

    public static async Task Until(Func<Task<bool>> predicate)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(40));
        while (!await predicate()) await Task.Delay(100, timeout.Token);
    }

    public async ValueTask DisposeAsync()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try { await App.StopAsync(timeout.Token); }
        finally { await App.DisposeAsync(); }
    }
}

// Real Inventory application/SQL/outbox/consumers; no mock fulfillment of ReserveInventory.
internal sealed class RealInventoryRuntime : IAsyncDisposable
{
    private readonly IHost _host;
    public string Prefix { get; } = $"inventory-e2e-{Guid.NewGuid():N}-";
    public string CommandQueue => Prefix + InventoryQueues.Commands;
    public OrderTestClock Clock { get; }
    private RealInventoryRuntime(IHost host, string prefix, OrderTestClock clock)
        => (_host, Prefix, Clock) = (host, prefix, clock);

    public static async Task<RealInventoryRuntime> Create(OrderContainersFixture fixture, OrderTestClock clock)
    {
        var prefix = $"inventory-e2e-{Guid.NewGuid():N}-";
        var builder = Host.CreateApplicationBuilder();
        builder.Logging.SetMinimumLevel(LogLevel.Error);
        var settings = OrderInventoryTestHost.RabbitSettings(fixture.RabbitMqConnectionString);
        settings["ConnectionStrings:InventoryServiceConnection"] = new SqlConnectionStringBuilder(fixture.DatabaseConnectionString)
            { InitialCatalog = $"InventoryE2E_{Guid.NewGuid():N}" }.ConnectionString;
        settings["Inventory:QueuePrefix"] = prefix;
        settings["Inventory:ExpiryEnabled"] = "false";
        settings["InventoryOutbox:DeliveryEnabled"] = "true";
        builder.Configuration.AddInMemoryCollection(settings);
        builder.Services.AddSingleton<TimeProvider>(clock);
        builder.Services.AddInventory(builder.Configuration);
        var host = builder.Build();
        try
        {
            await using var scope = host.Services.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<InventoryDbContext>().Database.MigrateAsync();
            return new(host, prefix, clock);
        }
        catch { host.Dispose(); throw; }
    }

    public Task Start() => _host.StartAsync();
    public async Task<Guid> Seed(int quantity)
    {
        var product = Guid.NewGuid();
        await using (var scope = _host.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<InventoryOperations>().RegisterProductAsync(product);
        if (quantity > 0)
        {
            await using var scope = _host.Services.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<InventoryOperations>()
                .ReceiveStockAsync(new(product, quantity, Guid.NewGuid()));
        }
        return product;
    }
    public Task Expire() => _host.Services.GetRequiredService<ReservationExpiryWorker>().RunOnceAsync(default);
    public async Task<int> Reserved(Guid product)
    {
        await using var scope = _host.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<InventoryDbContext>().InventoryItems
            .Where(x => x.ProductId == product).Select(x => x.ReservedQuantity).SingleAsync();
    }
    public async ValueTask DisposeAsync()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try { await _host.StopAsync(timeout.Token); }
        finally { _host.Dispose(); }
    }
}

internal sealed class ReserveCommandProbe : IAsyncDisposable
{
    private readonly IHost _host;
    private bool _started;
    private readonly Channel<(ReserveInventory Message, Guid? MessageId, Guid? CorrelationId)> _received =
        Channel.CreateUnbounded<(ReserveInventory, Guid?, Guid?)>();
    public ReserveCommandProbe(string rabbit, string queue)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Logging.SetMinimumLevel(LogLevel.Error);
        builder.Services.AddMassTransit(bus => bus.UsingRabbitMq((_, config) =>
        {
            config.Host(new Uri(rabbit));
            config.ReceiveEndpoint(queue, endpoint =>
            {
                endpoint.ConfigureConsumeTopology = false;
                endpoint.Handler<ReserveInventory>(context => _received.Writer.WriteAsync(
                    (context.Message, context.MessageId, context.CorrelationId), context.CancellationToken).AsTask());
            });
        }));
        _host = builder.Build();
    }
    public async Task Start()
    {
        LogContext.ConfigureCurrentLogContext(_host.Services.GetRequiredService<ILoggerFactory>());
        await _host.StartAsync();
        _started = true;
    }
    public async Task<(ReserveInventory Message, Guid? MessageId, Guid? CorrelationId)> Next()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        return await _received.Reader.ReadAsync(timeout.Token);
    }
    public async ValueTask DisposeAsync()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try { if (_started) await _host.StopAsync(timeout.Token); }
        finally { _host.Dispose(); }
    }
}
