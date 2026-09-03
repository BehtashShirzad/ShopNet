using Application.Abstractions.Contracts;
using Application.Abstractions;
using Domain.Abstractions;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrderService.Application.IntegrationEventHandler;
using OrderService.Application.Inventory;
using OrderService.Domain;

namespace OrderService.Infrastructure;

public static class DependencyServices
{
    public static void AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<WriteDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("OrderServiceConnection")));
        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<WriteDbContext>());
        services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<WriteDbContext>());
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IDomainEventDispatcher, MediatrDomainEventDispatcher>();
        services.AddScoped<IDomainEventBus, DomainEventBus>();
        services.AddScoped<IIntegrationEventBus, Bus>();
        services.AddScoped<IInventoryCommandSender, InventoryCommandSender>();
        services.AddScoped<IOrderTransactionLock, SqlOrderTransactionLock>();
        services.TryAddSingleton(TimeProvider.System);
        var inventoryOptions = new OrderInventoryOptions
        {
            ReservationDuration = TimeSpan.FromMinutes(int.Parse(configuration["Inventory:ReservationMinutes"] ?? "15",
                System.Globalization.CultureInfo.InvariantCulture))
        };
        inventoryOptions.Validate();
        services.AddSingleton(inventoryOptions);
        services.Configure<MassTransitHostOptions>(options =>
        {
            options.WaitUntilStarted = true;
            options.StartTimeout = TimeSpan.FromSeconds(30);
            options.StopTimeout = TimeSpan.FromSeconds(30);
        });
        services.AddMassTransit(bus =>
        {
            bus.AddConsumer<CartCheckedOutEventHandler>();
            bus.AddConsumer<InventoryResultConsumer>();
            bus.AddEntityFrameworkOutbox<WriteDbContext>(outbox =>
            {
                outbox.UseSqlServer();
                outbox.QueryDelay = TimeSpan.FromSeconds(1);
                outbox.UseBusOutbox(delivery =>
                {
                    if (!bool.TryParse(configuration["OrderOutbox:DeliveryEnabled"], out var enabled) || !enabled)
                        delivery.DisableDeliveryService();
                });
            });
            bus.UsingRabbitMq((context, rabbit) =>
            {
                var settings = configuration.GetSection("RabbitMq");
                rabbit.Host(settings["Host"] ?? "localhost", ushort.Parse(settings["Port"] ?? "5672"),
                    settings["VirtualHost"] ?? "/", host =>
                    {
                        host.Username(settings["Username"] ?? "guest");
                        host.Password(settings["Password"] ?? "guest");
                    });
                var prefix = configuration["Order:QueuePrefix"] ?? "";
                // Preserve the existing queue name for legacy Cart checkout events.
                rabbit.ReceiveEndpoint(prefix + "cart-checked-out-event-handler", endpoint =>
                {
                    endpoint.UseMessageRetry(retry => { retry.Ignore<ArgumentException>(); retry.Interval(3, TimeSpan.FromSeconds(1)); });
                    endpoint.ConfigureConsumer<CartCheckedOutEventHandler>(context);
                });
                rabbit.ReceiveEndpoint(prefix + "order-inventory-results-v1", endpoint =>
                {
                    endpoint.UseMessageRetry(retry => { retry.Ignore<ArgumentException>(); retry.Interval(3, TimeSpan.FromSeconds(1)); });
                    endpoint.ConfigureConsumer<InventoryResultConsumer>(context);
                });
            });
        });
    }
}
