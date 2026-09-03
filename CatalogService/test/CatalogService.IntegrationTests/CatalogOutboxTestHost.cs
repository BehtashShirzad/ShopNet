using System.Threading.Channels;
using CatalogService.Api.Grpc;
using CatalogService.Api.Routes;
using CatalogService.Application;
using CatalogService.Domain.Entities;
using CatalogService.Infrastructure;
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
using ProductCreatedV1 = ShopNet.Contracts.IntegrationEvents.Catalog.V1.ProductCreated;

namespace CatalogService.IntegrationTests;

/// <summary>Uses the production DI, domain dispatcher, repositories, bus adapter and outbox.</summary>
internal sealed class CatalogOutboxTestHost : IAsyncDisposable
{
    private CatalogOutboxTestHost(WebApplication app, string connectionString)
    {
        App = app;
        ConnectionString = connectionString;
    }

    public WebApplication App { get; }
    public string ConnectionString { get; }

    public static async Task<CatalogOutboxTestHost> Create(
        CatalogContainersFixture fixture,
        bool deliveryEnabled = false,
        string? connectionString = null,
        bool migrate = true)
    {
        connectionString ??= new SqlConnectionStringBuilder(fixture.DatabaseConnectionString)
        {
            InitialCatalog = $"CatalogOutbox_{Guid.NewGuid():N}"
        }.ConnectionString;
        var rabbitUri = new Uri(fixture.RabbitMqConnectionString);
        var credentials = rabbitUri.UserInfo.Split(':', 2);
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Testing"
        });
        builder.WebHost.UseTestServer();
        builder.Logging.SetMinimumLevel(LogLevel.Warning);
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:CatalogServiceConnection"] = connectionString,
            ["RabbitMq:Host"] = rabbitUri.Host,
            ["RabbitMq:Port"] = rabbitUri.Port.ToString(),
            ["RabbitMq:VirtualHost"] = Uri.UnescapeDataString(rabbitUri.AbsolutePath.TrimStart('/')) is { Length: > 0 } vhost
                ? vhost : "/",
            ["RabbitMq:Username"] = Uri.UnescapeDataString(credentials[0]),
            ["RabbitMq:Password"] = Uri.UnescapeDataString(credentials[1]),
            ["CatalogOutbox:DeliveryEnabled"] = deliveryEnabled.ToString()
        });
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddGrpc();
        builder.Services.AddApplicationServices();
        builder.Services.AddInfraService(builder.Configuration);
        builder.Services.Configure<MassTransitHostOptions>(options =>
        {
            options.WaitUntilStarted = true;
            options.StartTimeout = TimeSpan.FromSeconds(30);
            options.StopTimeout = TimeSpan.FromSeconds(30);
        });
        var app = builder.Build();
        app.MapProductEndpoints();
        app.MapGrpcService<CatalogServiceGrpcService>();

        try
        {
            if (migrate)
            {
                await using var scope = app.Services.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<WriteDbContext>()
                    .Database.MigrateAsync();
            }
            return new CatalogOutboxTestHost(app, connectionString);
        }
        catch
        {
            await app.DisposeAsync();
            throw;
        }
    }

    public Task Start() => App.StartAsync();

    public async Task<Guid> SeedCategory()
    {
        await using var scope = App.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<WriteDbContext>();
        var category = CategoryEntity.Create($"Category-{Guid.NewGuid():N}");
        context.Categories.Add(category);
        await context.SaveChangesAsync();
        return category.Id;
    }

    public async Task<int> PendingMessages()
    {
        await using var scope = App.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<WriteDbContext>()
            .Set<OutboxMessage>().CountAsync();
    }

    public async Task WaitUntilOutboxDrained()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        while (await PendingMessages() != 0)
            await Task.Delay(100, timeout.Token);
    }

    public async ValueTask DisposeAsync()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try { await App.StopAsync(timeout.Token); }
        finally { await App.DisposeAsync(); }
    }
}

internal sealed class ProductCreatedReceiver : IAsyncDisposable
{
    private readonly Channel<ProductCreatedV1> _messages = Channel.CreateUnbounded<ProductCreatedV1>();
    private readonly IBusControl _bus;

    public ProductCreatedReceiver(string connectionString)
    {
        _bus = MassTransit.Bus.Factory.CreateUsingRabbitMq(configuration =>
        {
            configuration.Host(new Uri(connectionString));
            configuration.ReceiveEndpoint($"catalog-created-v1-test-{Guid.NewGuid():N}", endpoint =>
            {
                endpoint.Durable = false;
                endpoint.AutoDelete = true;
                endpoint.Handler<ProductCreatedV1>(context =>
                    _messages.Writer.WriteAsync(context.Message, context.CancellationToken).AsTask());
            });
        });
    }

    public Task Start() => _bus.StartAsync();

    public async Task<ProductCreatedV1> Receive(Guid productId)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        while (true)
        {
            var message = await _messages.Reader.ReadAsync(timeout.Token);
            if (message.ProductId == productId)
                return message;
        }
    }

    public async ValueTask DisposeAsync()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await _bus.StopAsync(timeout.Token);
    }
}
