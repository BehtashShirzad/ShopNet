using System.Threading.Channels;
using Grpc.Net.Client;
using InventoryService.Api.Grpc;
using InventoryService.Application;
using InventoryService.Domain.Aggregates;
using InventoryService.Infrastructure;
using MassTransit;
using MassTransit.EntityFrameworkCoreIntegration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ShopNet.Contracts;
using ShopNet.Contracts.Inventory.V1;
using Testcontainers.MsSql;
using Testcontainers.RabbitMq;

[assembly: CollectionBehavior(DisableTestParallelization = true)]
namespace InventoryService.IntegrationTests;

public sealed class InventoryContainers : IAsyncLifetime
{
    private readonly MsSqlContainer _sql = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-CU14-ubuntu-22.04").Build();
    private readonly RabbitMqContainer _rabbit = new RabbitMqBuilder("rabbitmq:3.13-alpine").Build();
    public string Sql => _sql.GetConnectionString();
    public string Rabbit => _rabbit.GetConnectionString();
    public Task InitializeAsync() => Task.WhenAll(_sql.StartAsync(), _rabbit.StartAsync());
    public async Task DisposeAsync()
    {
        await _rabbit.DisposeAsync();
        await _sql.DisposeAsync();
    }
}

[CollectionDefinition("Inventory containers")]
public sealed class InventoryCollection : ICollectionFixture<InventoryContainers>;

internal sealed class MutableClock : TimeProvider
{
    public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;
    public override DateTimeOffset GetUtcNow() => Now;
}

internal sealed class InventoryTestHost : IAsyncDisposable
{
    public WebApplication App { get; }
    public string ConnectionString { get; }
    public string Prefix { get; }
    public MutableClock Clock { get; }
    public IServiceProvider Services => App.Services;

    private InventoryTestHost(WebApplication app, string connection, string prefix, MutableClock clock)
        => (App, ConnectionString, Prefix, Clock) = (app, connection, prefix, clock);

    public static async Task<InventoryTestHost> Create(InventoryContainers containers, bool deliver = false,
        string? connection = null, string? prefix = null, MutableClock? clock = null)
    {
        connection ??= new SqlConnectionStringBuilder(containers.Sql)
            { InitialCatalog = $"Inventory_{Guid.NewGuid():N}" }.ConnectionString;
        prefix ??= $"test-{Guid.NewGuid():N}-";
        clock ??= new MutableClock();
        var rabbit = new Uri(containers.Rabbit);
        var credentials = rabbit.UserInfo.Split(':', 2);
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
        builder.WebHost.UseTestServer();
        builder.Logging.SetMinimumLevel(LogLevel.Error);
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:InventoryServiceConnection"] = connection,
            ["RabbitMq:Host"] = rabbit.Host, ["RabbitMq:Port"] = rabbit.Port.ToString(),
            ["RabbitMq:VirtualHost"] = "/",
            ["RabbitMq:Username"] = Uri.UnescapeDataString(credentials[0]),
            ["RabbitMq:Password"] = Uri.UnescapeDataString(credentials[1]),
            ["Inventory:QueuePrefix"] = prefix, ["Inventory:ExpiryEnabled"] = "false",
            ["InventoryOutbox:DeliveryEnabled"] = deliver.ToString()
        });
        builder.Services.AddSingleton<TimeProvider>(clock);
        builder.Services.AddGrpc();
        builder.Services.AddInventory(builder.Configuration);
        builder.Services.Configure<MassTransitHostOptions>(options =>
        {
            options.WaitUntilStarted = true;
            options.StartTimeout = TimeSpan.FromSeconds(30);
            options.StopTimeout = TimeSpan.FromSeconds(30);
        });
        var app = builder.Build();
        app.MapGrpcService<InventoryAvailabilityGrpcService>();
        try
        {
            await using var scope = app.Services.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<InventoryDbContext>().Database.MigrateAsync();
            return new(app, connection, prefix, clock);
        }
        catch { await app.DisposeAsync(); throw; }
    }

    public Task Start() => App.StartAsync();
    public async Task Run(Func<InventoryOperations, Task> action)
    {
        await using var scope = Services.CreateAsyncScope();
        await action(scope.ServiceProvider.GetRequiredService<InventoryOperations>());
    }

    public async Task<Guid> Seed(int stock = 10)
    {
        var id = Guid.NewGuid();
        await Run(x => x.RegisterProductAsync(id));
        if (stock > 0) await Run(x => x.ReceiveStockAsync(new(id, stock, Guid.NewGuid())));
        return id;
    }

    public ReserveInventory Request(params InventoryLine[] items)
        => new(Guid.NewGuid(), Guid.NewGuid(), items, Clock.GetUtcNow().AddMinutes(10));

    public async Task<InventoryItem?> Item(Guid productId)
    {
        await using var scope = Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<InventoryDbContext>().InventoryItems.AsNoTracking()
            .Include(x => x.Reservations).SingleOrDefaultAsync(x => x.ProductId == productId);
    }

    public async Task<ReservationAttempt?> Attempt(Guid requestId)
    {
        await using var scope = Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<InventoryDbContext>().ReservationAttempts.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == requestId);
    }

    public async Task<int> Pending()
    {
        await using var scope = Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<InventoryDbContext>().Set<OutboxMessage>().CountAsync();
    }

    public static async Task Until(Func<Task<bool>> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        while (!await condition()) await Task.Delay(100, timeout.Token);
    }

    public GrpcChannel Channel() => GrpcChannel.ForAddress("http://localhost",
        new GrpcChannelOptions { HttpHandler = App.GetTestServer().CreateHandler() });

    public async ValueTask DisposeAsync()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try { await App.StopAsync(timeout.Token); }
        finally { await App.DisposeAsync(); }
    }
}

internal sealed class InventoryMessageProbe : IAsyncDisposable
{
    private readonly Channel<IntegrationEvent> _events = Channel.CreateUnbounded<IntegrationEvent>();
    private readonly IBusControl _bus;
    public InventoryMessageProbe(string connection)
    {
        _bus = Bus.Factory.CreateUsingRabbitMq(rabbit =>
        {
            rabbit.Host(new Uri(connection));
            rabbit.ReceiveEndpoint($"inventory-results-test-{Guid.NewGuid():N}", endpoint =>
            {
                endpoint.AutoDelete = true;
                endpoint.Durable = false;
                endpoint.Handler<InventoryReserved>(Receive);
                endpoint.Handler<InventoryRejected>(Receive);
                endpoint.Handler<InventoryCommitted>(Receive);
                endpoint.Handler<InventoryReleased>(Receive);
                endpoint.Handler<InventoryExpired>(Receive);
                endpoint.Handler<InventoryCommandRejected>(Receive);
            });
        });
    }
    private Task Receive<T>(ConsumeContext<T> context) where T : IntegrationEvent
    {
        Assert.Equal(context.Message.EventId, context.MessageId);
        return _events.Writer.WriteAsync(context.Message, context.CancellationToken).AsTask();
    }
    public Task Start() => _bus.StartAsync();
    public Task Publish<T>(T message) where T : class => _bus.Publish(message);
    public async Task Send<T>(string queue, T message) where T : class
        => await (await _bus.GetSendEndpoint(new Uri("queue:" + queue))).Send(message);
    public async Task<T> Next<T>(Func<T, bool> predicate) where T : IntegrationEvent
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        while (true)
            if (await _events.Reader.ReadAsync(timeout.Token) is T result && predicate(result)) return result;
    }
    public async ValueTask DisposeAsync()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await _bus.StopAsync(timeout.Token);
    }
}
