using Application.Abstractions.Contracts;
using CartService.Application;
using CartService.Application.Checkout;
using CartService.Domain;
using CatalogService.API.Grpc.Protos;
using InventoryService.Grpc.V1;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StackExchange.Redis;

namespace CartService.Infrastructure;

public static class Dependency
{
    public static void AddInfraServices(this IServiceCollection services, IConfiguration cfg)
    {
        services.AddScoped<IRedisService, RedisService>();
        services.AddScoped<CartServiceRepository>();
        services.AddScoped<IRepository>(provider => provider.GetRequiredService<CartServiceRepository>());
        services.AddScoped<ICartCheckoutStore>(provider => provider.GetRequiredService<CartServiceRepository>());
        services.AddSingleton(new CartRedisOptions { KeyPrefix = cfg["Redis:KeyPrefix"] ?? "" });
        services.AddSingleton<CartRedisPersistence>();
        services.AddSingleton<ICartRedisPersistence>(provider => provider.GetRequiredService<CartRedisPersistence>());
        services.AddSingleton<ICheckoutOutbox>(provider => provider.GetRequiredService<CartRedisPersistence>());
        services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(
            ConfigurationOptions.Parse(cfg["Redis:ConnectionString"] ?? cfg["Redis:Configuration"] ?? "localhost:6379", true)));
        services.TryAddSingleton(TimeProvider.System);
        var grpc = new GrpcCallOptions
        {
            Timeout = TimeSpan.FromSeconds(int.Parse(cfg["Grpc:TimeoutSeconds"] ?? "5",
                System.Globalization.CultureInfo.InvariantCulture))
        };
        grpc.Validate();
        services.AddSingleton(grpc);
        services.AddGrpcClient<CatalogProtoService.CatalogProtoServiceClient>(options =>
            options.Address = new Uri(cfg["Grpc:CatalogService"] ?? "http://localhost:60002"));
        services.AddGrpcClient<InventoryAvailabilityService.InventoryAvailabilityServiceClient>(options =>
            options.Address = new Uri(cfg["Grpc:InventoryService"] ?? "http://localhost:5084"));
        services.AddScoped<ICatalogService, CatalogGrpcClient>();
        services.AddScoped<IInventoryAvailabilityClient, InventoryGrpcClient>();
        services.AddScoped<IIntegrationEventBus, Bus>();
        services.AddSingleton<ICheckoutPublisher, CheckoutPublisher>();
        services.AddSingleton<CartOutboxDispatcher>();
        services.Configure<MassTransitHostOptions>(options =>
        {
            options.WaitUntilStarted = true;
            options.StartTimeout = TimeSpan.FromSeconds(30);
            options.StopTimeout = TimeSpan.FromSeconds(30);
        });
        services.AddMassTransit(bus => bus.UsingRabbitMq((_, rabbit) =>
        {
            var settings = cfg.GetSection("RabbitMq");
            rabbit.Host(settings["Host"] ?? "localhost", ushort.Parse(settings["Port"] ?? "5672"),
                settings["VirtualHost"] ?? "/", host =>
                {
                    host.Username(settings["Username"] ?? "guest");
                    host.Password(settings["Password"] ?? "guest");
                });
        }));
        if (bool.TryParse(cfg["CartOutbox:DeliveryEnabled"], out var enabled) && enabled)
            services.AddHostedService(provider => provider.GetRequiredService<CartOutboxDispatcher>());
    }
}
