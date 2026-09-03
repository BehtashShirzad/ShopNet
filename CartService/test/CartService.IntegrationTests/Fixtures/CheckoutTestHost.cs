using System.Security.Claims;
using System.Text.Encodings.Web;
using CartService.Api;
using CartService.Application;
using CartService.Application.Commands;
using CartService.Domain;
using CartService.Domain.Aggregates;
using CartService.Infrastructure;
using CatalogService.API.Grpc.Protos;
using InventoryService.Grpc.V1;
using MassTransit;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace CartService.IntegrationTests;

internal sealed class CheckoutTestHost : IAsyncDisposable
{
    public static readonly Guid Customer = Guid.Parse("00000000-0000-0000-0000-000000000011");
    public WebApplication App { get; }
    private readonly WebApplication _grpc;
    public CheckoutGrpcState Upstream { get; }
    public IServiceProvider Services => App.Services;
    public CartRedisOptions Keys => Services.GetRequiredService<CartRedisOptions>();
    public IDatabase Redis => Services.GetRequiredService<IConnectionMultiplexer>().GetDatabase();
    public ICheckoutOutbox Outbox => Services.GetRequiredService<ICheckoutOutbox>();
    public HttpClient Http => App.GetTestClient();
    private CheckoutTestHost(WebApplication app, WebApplication grpc, CheckoutGrpcState state)
        => (App, _grpc, Upstream) = (app, grpc, state);

    public static Dictionary<string, string?> RabbitSettings(string connection)
    {
        var uri = new Uri(connection);
        var credentials = uri.UserInfo.Split(':', 2);
        return new()
        {
            ["RabbitMq:Host"] = uri.Host, ["RabbitMq:Port"] = uri.Port.ToString(), ["RabbitMq:VirtualHost"] = "/",
            ["RabbitMq:Username"] = Uri.UnescapeDataString(credentials[0]),
            ["RabbitMq:Password"] = Uri.UnescapeDataString(credentials[1])
        };
    }

    public static async Task<CheckoutTestHost> Create(CartContainersFixture fixture, string? prefix = null, bool delivery = false)
    {
        var state = new CheckoutGrpcState();
        var grpcBuilder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
        grpcBuilder.WebHost.UseTestServer();
        grpcBuilder.Logging.SetMinimumLevel(LogLevel.None);
        grpcBuilder.Services.AddGrpc();
        grpcBuilder.Services.AddSingleton(state);
        var grpc = grpcBuilder.Build();
        grpc.MapGrpcService<CatalogGrpcStub>();
        grpc.MapGrpcService<InventoryGrpcStub>();
        await grpc.StartAsync();
        WebApplication? app = null;
        try
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
            builder.WebHost.UseTestServer();
            builder.Logging.SetMinimumLevel(LogLevel.None);
            var settings = RabbitSettings(fixture.RabbitMqConnectionString);
            settings["Redis:ConnectionString"] = fixture.RedisConnectionString;
            settings["Redis:KeyPrefix"] = prefix ?? $"cart-test:{Guid.NewGuid():N}:";
            settings["CartOutbox:DeliveryEnabled"] = delivery.ToString();
            settings["Grpc:TimeoutSeconds"] = "1";
            settings["Grpc:CatalogService"] = "http://localhost";
            settings["Grpc:InventoryService"] = "http://localhost";
            builder.Configuration.AddInMemoryCollection(settings);
            builder.Services.AddApplicationServices();
            builder.Services.AddInfraServices(builder.Configuration);
            builder.Services.AddGrpcClient<CatalogProtoService.CatalogProtoServiceClient>()
                .ConfigurePrimaryHttpMessageHandler(() => grpc.GetTestServer().CreateHandler());
            builder.Services.AddGrpcClient<InventoryAvailabilityService.InventoryAvailabilityServiceClient>()
                .ConfigurePrimaryHttpMessageHandler(() => grpc.GetTestServer().CreateHandler());
            builder.Services.AddHttpContextAccessor();
            builder.Services.AddAuthentication("test").AddScheme<AuthenticationSchemeOptions, CheckoutTestAuth>("test", _ => { });
            builder.Services.AddAuthorization();
            app = builder.Build();
            app.UseAuthentication();
            app.UseAuthorization();
            app.MapCartEndpoints();
            LogContext.ConfigureCurrentLogContext(app.Services.GetRequiredService<ILoggerFactory>());
            await app.StartAsync();
            return new(app, grpc, state);
        }
        catch
        {
            if (app is not null) await app.DisposeAsync();
            await grpc.DisposeAsync();
            throw;
        }
    }
    public async Task<CartAggregate> Seed()
    {
        var cart = CartAggregate.Create(Customer);
        cart.AddItem(Upstream.ProductId, "Product", 10, 2);
        await using var scope = Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IRepository>().StoreCart(cart);
        return cart;
    }
    public async Task<CartAggregate> Read(Guid id)
    {
        await using var scope = Services.CreateAsyncScope();
        return (await scope.ServiceProvider.GetRequiredService<IRepository>().GetCart(id))!;
    }
    public async Task<Guid> Checkout(Guid id)
    {
        await using var scope = Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(new CheckoutCartCommand(id, Customer));
    }
    public Task<long> PendingCount() => Redis.HashLengthAsync(Keys.MessagesKey);
    public async ValueTask DisposeAsync()
    {
        try { await App.StopAsync(); }
        finally { await App.DisposeAsync(); await _grpc.DisposeAsync(); }
    }
}

public sealed class CheckoutTestAuth(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (Request.Headers.ContainsKey("X-Test-Anonymous")) return Task.FromResult(AuthenticateResult.NoResult());
        var user = Request.Headers["X-Test-User"].FirstOrDefault() ?? CheckoutTestHost.Customer.ToString();
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(
            new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", user)], Scheme.Name)), Scheme.Name)));
    }
}
