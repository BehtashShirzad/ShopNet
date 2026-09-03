using InventoryService.Application;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ShopNet.Contracts.Inventory.V1;

namespace InventoryService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInventory(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<InventoryDbContext>(options => options.UseSqlServer(
            configuration.GetConnectionString("InventoryServiceConnection")
                ?? throw new InvalidOperationException("InventoryServiceConnection is required.")));
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<IInventoryStore, SqlInventoryStore>();
        services.AddScoped<IInventoryEventPublisher, InventoryEventPublisher>();
        services.AddScoped<InventoryOperations>();
        services.Configure<MassTransitHostOptions>(options =>
        {
            options.WaitUntilStarted = true;
            options.StartTimeout = TimeSpan.FromSeconds(30);
            options.StopTimeout = TimeSpan.FromSeconds(30);
        });
        services.AddSingleton<ReservationExpiryWorker>();
        if (!bool.TryParse(configuration["Inventory:ExpiryEnabled"], out var expiryEnabled) || expiryEnabled)
            services.AddHostedService(provider => provider.GetRequiredService<ReservationExpiryWorker>());

        services.AddMassTransit(bus =>
        {
            bus.AddConsumer<InventoryCommandConsumer>();
            bus.AddConsumer<ProductCreatedConsumer>();
            bus.AddConsumer<ReceiveStockConsumer>();
            bus.AddEntityFrameworkOutbox<InventoryDbContext>(outbox =>
            {
                outbox.UseSqlServer();
                outbox.QueryDelay = TimeSpan.FromSeconds(1);
                outbox.UseBusOutbox(delivery =>
                {
                    // Order has no durable result subscription yet. Retain results in SQL until cutover.
                    if (!bool.TryParse(configuration["InventoryOutbox:DeliveryEnabled"], out var enabled) || !enabled)
                        delivery.DisableDeliveryService();
                });
            });
            bus.UsingRabbitMq((context, rabbit) =>
            {
                var settings = configuration.GetSection("RabbitMq");
                rabbit.Host(settings["Host"] ?? "localhost",
                    ushort.Parse(settings["Port"] ?? "5672"), settings["VirtualHost"] ?? "/", host =>
                {
                    host.Username(settings["Username"] ?? "guest");
                    host.Password(settings["Password"] ?? "guest");
                });
                var prefix = configuration["Inventory:QueuePrefix"] ?? "";
                rabbit.ReceiveEndpoint(prefix + InventoryQueues.CatalogProducts, endpoint =>
                {
                    endpoint.UseMessageRetry(retry => { retry.Ignore<ArgumentException>(); retry.Interval(3, TimeSpan.FromSeconds(1)); });
                    endpoint.ConfigureConsumer<ProductCreatedConsumer>(context);
                });
                rabbit.ReceiveEndpoint(prefix + InventoryQueues.Commands, endpoint =>
                {
                    // Only explicitly addressed commands are accepted; no subscription to OrderCreated.
                    endpoint.ConfigureConsumeTopology = false;
                    endpoint.UseMessageRetry(retry => { retry.Ignore<ArgumentException>(); retry.Interval(3, TimeSpan.FromSeconds(1)); });
                    endpoint.ConfigureConsumer<InventoryCommandConsumer>(context);
                });
                rabbit.ReceiveEndpoint(prefix + InventoryQueues.StockReceipts, endpoint =>
                {
                    endpoint.ConfigureConsumeTopology = false;
                    endpoint.UseMessageRetry(retry => { retry.Ignore<ArgumentException>(); retry.Interval(3, TimeSpan.FromSeconds(1)); });
                    endpoint.ConfigureConsumer<ReceiveStockConsumer>(context);
                });
            });
        });
        return services;
    }
}
