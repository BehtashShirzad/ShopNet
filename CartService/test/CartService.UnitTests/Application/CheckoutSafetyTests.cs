using CartService.Application;
using CartService.Application.Checkout;
using CartService.Application.Commands;
using CartService.Application.Query;
using CartService.Domain;
using CartService.Domain.Aggregates;
using CartService.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Newtonsoft.Json;
using ShopNet.Contracts.IntegrationEvents;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Grpc.Net.ClientFactory;
using CatalogService.API.Grpc.Protos;
using InventoryService.Grpc.V1;

namespace CartService.UnitTests;

public sealed class CheckoutSafetyTests
{
    private static CartAggregate Cart()
    {
        var cart = CartAggregate.Create(Guid.NewGuid());
        cart.AddItem(Guid.NewGuid(), "Product", 10, 2);
        return cart;
    }

    [Fact]
    public void EmptyCheckoutIsRejected()
        => Assert.Throws<InvalidOperationException>(() => CartAggregate.Create(Guid.NewGuid()).Checkout());

    [Fact]
    public void CheckedOutCartIsImmutableAndRetryPreservesIdentity()
    {
        var cart = Cart();
        cart.Checkout();
        var id = cart.CheckoutEventId;
        var time = cart.CheckedOutAtUtc;
        cart.Checkout();
        Assert.Equal(id, cart.CheckoutEventId);
        Assert.Equal(time, cart.CheckedOutAtUtc);
        Assert.Throws<InvalidOperationException>(() => cart.AddItem(Guid.NewGuid(), "P", 1, 1));
        Assert.Throws<InvalidOperationException>(() => cart.RemoveItem(cart.Items.Single().ProductId));
        Assert.Throws<InvalidOperationException>(() => cart.ChangeItemQuantity(cart.Items.Single().ProductId, 3));
    }

    [Fact]
    public void QuantityOverflowDoesNotMutateCart()
    {
        var cart = Cart();
        var product = cart.Items.Single().ProductId;
        cart.ChangeItemQuantity(product, int.MaxValue);
        Assert.Throws<OverflowException>(() => cart.AddItem(product, "Product", 10, 1));
        Assert.Equal(int.MaxValue, cart.Items.Single().Quantity);
    }

    [Theory]
    [InlineData(0.001)]
    [InlineData(10000000000000000)]
    public void InvalidOrderPriceCannotEnterCart(decimal price)
        => Assert.Throws<ArgumentException>(() => CartAggregate.Create(Guid.NewGuid()).AddItem(Guid.NewGuid(), "P", price, 1));

    [Fact]
    public void StockIsAbsentFromCatalogApplicationDto()
        => Assert.Null(typeof(GetProductDto).GetProperty("Stock"));

    [Fact]
    public async Task CheckoutRetrySkipsAllDependenciesAndCannotReenqueue()
    {
        var cart = Cart();
        cart.Checkout();
        var repo = new Mock<IRepository>();
        repo.Setup(x => x.GetCart(cart.Id)).ReturnsAsync(cart);
        var result = await new CheckoutCartCommandHandler(repo.Object,
            new Mock<ICatalogService>(MockBehavior.Strict).Object,
            new Mock<IInventoryAvailabilityClient>(MockBehavior.Strict).Object,
            new Mock<ICartCheckoutStore>(MockBehavior.Strict).Object, TimeProvider.System)
            .Handle(new(cart.Id, cart.CustomerId), default);
        Assert.Equal(cart.Id, result);
    }

    [Theory]
    [InlineData("foreign")]
    [InlineData("missing")]
    [InlineData("empty")]
    public async Task InvalidCartCannotReachRemoteDependencies(string condition)
    {
        var cart = condition == "empty" ? CartAggregate.Create(Guid.NewGuid()) : Cart();
        var repo = new Mock<IRepository>();
        repo.Setup(x => x.GetCart(cart.Id)).ReturnsAsync(condition == "missing" ? null : cart);
        var handler = new CheckoutCartCommandHandler(repo.Object,
            new Mock<ICatalogService>(MockBehavior.Strict).Object,
            new Mock<IInventoryAvailabilityClient>(MockBehavior.Strict).Object,
            new Mock<ICartCheckoutStore>(MockBehavior.Strict).Object, TimeProvider.System);
        await Assert.ThrowsAsync<CheckoutRejectedException>(() => handler.Handle(
            new(cart.Id, condition == "foreign" ? Guid.NewGuid() : cart.CustomerId), default));
    }

    [Theory]
    [InlineData("price")]
    [InlineData("missing")]
    [InlineData("identity")]
    public async Task CatalogMismatchRejectsWithoutChangingCart(string kind)
    {
        var cart = Cart();
        var product = cart.Items.Single().ProductId;
        var repo = new Mock<IRepository>();
        repo.Setup(x => x.GetCart(cart.Id)).ReturnsAsync(cart);
        var catalog = new Mock<ICatalogService>();
        catalog.Setup(x => x.GetProduct(product, It.IsAny<CancellationToken>()))
            .ReturnsAsync(kind == "missing" ? null : new GetProductDto(
                kind == "identity" ? Guid.NewGuid() : product, "P", kind == "price" ? 11 : 10));
        await Assert.ThrowsAsync<CheckoutRejectedException>(() => new CheckoutCartCommandHandler(repo.Object,
            catalog.Object, new Mock<IInventoryAvailabilityClient>(MockBehavior.Strict).Object,
            new Mock<ICartCheckoutStore>(MockBehavior.Strict).Object, TimeProvider.System)
            .Handle(new(cart.Id, cart.CustomerId), default));
        Assert.False(cart.IsCheckedOut);
        Assert.Equal(10, cart.Items.Single().Price);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("inactive")]
    [InlineData("unknown")]
    [InlineData("short")]
    public async Task InventoryMustExplicitlyApproveEveryLine(string kind)
    {
        var cart = Cart();
        var product = cart.Items.Single().ProductId;
        var repo = new Mock<IRepository>();
        repo.Setup(x => x.GetCart(cart.Id)).ReturnsAsync(cart);
        var catalog = new Mock<ICatalogService>();
        catalog.Setup(x => x.GetProduct(product, It.IsAny<CancellationToken>())).ReturnsAsync(new GetProductDto(product, "P", 10));
        var inventory = new Mock<IInventoryAvailabilityClient>();
        var stocks = new Dictionary<Guid, InventoryAvailability>();
        if (kind != "missing") stocks[product] = new(product, kind != "unknown", kind != "inactive", kind == "short" ? 1 : 10);
        inventory.Setup(x => x.GetAvailabilityAsync(It.IsAny<Guid[]>(), It.IsAny<CancellationToken>())).ReturnsAsync(stocks);
        var exception = await Assert.ThrowsAsync<CheckoutRejectedException>(() => new CheckoutCartCommandHandler(
            repo.Object, catalog.Object, inventory.Object, new Mock<ICartCheckoutStore>(MockBehavior.Strict).Object,
            TimeProvider.System).Handle(new(cart.Id, cart.CustomerId), default));
        Assert.Equal("insufficient_stock", exception.Code);
        Assert.False(cart.IsCheckedOut);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OutboxFailureRetainsPayloadAndSuccessAcknowledges(bool fail)
    {
        var message = new CartCheckedOutEvent(Guid.NewGuid(), Guid.NewGuid(), [], 0);
        var lease = new CheckoutLease(message.EventId, JsonConvert.SerializeObject(message), "token");
        var outbox = new Mock<ICheckoutOutbox>();
        outbox.SetupSequence(x => x.ClaimAsync(It.IsAny<CancellationToken>())).ReturnsAsync(lease).ReturnsAsync((CheckoutLease?)null);
        outbox.Setup(x => x.AcknowledgeAsync(lease, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var publisher = new Mock<ICheckoutPublisher>();
        if (fail) publisher.Setup(x => x.PublishAsync(It.IsAny<CartCheckedOutEvent>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Broker unavailable"));
        var result = await new CartOutboxDispatcher(outbox.Object, publisher.Object, NullLogger<CartOutboxDispatcher>.Instance)
            .RunOnceAsync(default);
        Assert.Equal(fail ? 0 : 1, result);
        outbox.Verify(x => x.RetryAsync(lease, It.IsAny<CancellationToken>()), fail ? Times.Once() : Times.Never());
        outbox.Verify(x => x.AcknowledgeAsync(lease, It.IsAny<CancellationToken>()), fail ? Times.Never() : Times.Once());
    }

    [Fact]
    public async Task CorruptOutboxPayloadIsRetainedNotPublished()
    {
        var lease = new CheckoutLease(Guid.NewGuid(), "not-json", "token");
        var outbox = new Mock<ICheckoutOutbox>();
        outbox.SetupSequence(x => x.ClaimAsync(It.IsAny<CancellationToken>())).ReturnsAsync(lease).ReturnsAsync((CheckoutLease?)null);
        var publisher = new Mock<ICheckoutPublisher>(MockBehavior.Strict);
        Assert.Equal(0, await new CartOutboxDispatcher(outbox.Object, publisher.Object, NullLogger<CartOutboxDispatcher>.Instance)
            .RunOnceAsync(default));
        outbox.Verify(x => x.RetryAsync(lease, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task NormalRepositoryStoreCannotBypassCheckoutOutbox()
    {
        var cart = Cart();
        cart.Checkout();
        var storage = new Mock<ICartRedisPersistence>(MockBehavior.Strict);
        await Assert.ThrowsAsync<InvalidOperationException>(() => new CartServiceRepository(storage.Object).StoreCart(cart));
    }

    [Fact]
    public void CartLimitsDistinctProductsButCanIncreaseAnExistingLine()
    {
        var cart = CartAggregate.Create(Guid.NewGuid());
        for (var i = 0; i < 100; i++) cart.AddItem(Guid.NewGuid(), "P", 1, 1);
        var first = cart.Items.First().ProductId;
        cart.AddItem(first, "P", 1, 1);
        Assert.Equal(2, cart.Items.First().Quantity);
        Assert.Throws<ArgumentException>(() => cart.AddItem(Guid.NewGuid(), "P", 1, 1));
        Assert.Equal(100, cart.Items.Count);
    }

    [Fact]
    public void CheckoutRejectsEmptyIdentityAndNormalizesTime()
    {
        var cart = Cart();
        Assert.Throws<ArgumentException>(() => cart.Checkout(Guid.Empty, DateTimeOffset.UtcNow));
        Assert.False(cart.IsCheckedOut);
        var now = new DateTimeOffset(2026, 9, 3, 14, 0, 0, TimeSpan.FromHours(3.5));
        cart.Checkout(Guid.NewGuid(), now);
        Assert.Equal(TimeSpan.Zero, cart.CheckedOutAtUtc!.Value.Offset);
        Assert.Equal(now, cart.CheckedOutAtUtc);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(31)]
    public void InvalidGrpcTimeoutFailsStartup(int seconds)
        => Assert.Throws<ArgumentException>(() => new GrpcCallOptions { Timeout = TimeSpan.FromSeconds(seconds) }.Validate());

    [Theory]
    [InlineData("null")]
    [InlineData("quantity")]
    [InlineData("too-many")]
    public async Task InvalidAddCartCannotReachCatalogOrStorage(string condition)
    {
        List<ProductViewModelInput> products = condition switch
        {
            "null" => null!,
            "quantity" => [new(Guid.NewGuid(), 0)],
            _ => Enumerable.Range(0, 101).Select(_ => new ProductViewModelInput(Guid.NewGuid(), 1)).ToList()
        };
        var handler = new AddCartCommandHandler(new Mock<IRepository>(MockBehavior.Strict).Object,
            new Mock<ICatalogService>(MockBehavior.Strict).Object);
        await Assert.ThrowsAsync<ArgumentException>(() => handler.Handle(new(products) { UserId = Guid.NewGuid() }, default));
    }

    [Fact]
    public async Task AddProductRejectsCheckedOutCartBeforeCatalogCall()
    {
        var cart = Cart();
        cart.Checkout();
        var repo = new Mock<IRepository>();
        repo.Setup(x => x.GetCart(cart.Id)).ReturnsAsync(cart);
        var handler = new AddProductToCartCommandHandler(repo.Object, new Mock<ICatalogService>(MockBehavior.Strict).Object);
        var error = await Assert.ThrowsAsync<CheckoutRejectedException>(() => handler.Handle(new()
        {
            CartId = cart.Id, UserId = cart.CustomerId,
            ProductDto = new(cart.Items.Single().ProductId, 1)
        }, default));
        Assert.Equal("cart_closed", error.Code);
    }

    [Fact]
    public async Task OutboxRefusesPayloadThatDoesNotMatchSnapshot()
    {
        var cart = Cart();
        cart.Checkout();
        var storage = new Mock<ICartRedisPersistence>(MockBehavior.Strict);
        var mismatch = new CartCheckedOutEvent(cart.Id, cart.CustomerId, [], cart.TotalPrice)
        { EventId = cart.CheckoutEventId!.Value, OccurredOnUtc = cart.CheckedOutAtUtc!.Value };
        await Assert.ThrowsAsync<InvalidOperationException>(() => new CartServiceRepository(storage.Object)
            .CompleteAsync(cart, mismatch, default));
    }

    [Fact]
    public async Task QueryExposesStableCheckoutIdentity()
    {
        var cart = Cart();
        cart.Checkout();
        var repo = new Mock<IRepository>();
        repo.Setup(x => x.GetCart(cart.Id)).ReturnsAsync(cart);
        var result = await new UserCartQueryHandler(repo.Object).Handle(new(cart.Id, cart.CustomerId), default);
        Assert.True(result.IsCheckedOut);
        Assert.Equal(cart.CheckoutEventId, result.CheckoutEventId);
    }

    [Fact]
    public void RedisDefaultCartKeyRemainsBackwardCompatible()
    {
        var id = Guid.NewGuid();
        Assert.Equal(id.ToString(), new CartRedisOptions().CartKey(id));
    }

    [Fact]
    public void DefaultGrpcAddressesUseTheActualHttp2Ports()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfraServices(new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["Keycloak:ServiceClient:TokenEndpoint"] = "http://keycloak/token",
                ["Keycloak:ServiceClient:ClientId"] = "cart-service",
                ["Keycloak:ServiceClient:ClientSecret"] = "test-secret"
            }).Build());
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptionsMonitor<GrpcClientFactoryOptions>>();
        Assert.Equal(60002, options.Get(nameof(CatalogProtoService.CatalogProtoServiceClient)).Address!.Port);
        Assert.Equal(5084, options.Get(nameof(InventoryAvailabilityService.InventoryAvailabilityServiceClient)).Address!.Port);
    }

    [Fact]
    public void ProductionSettingsAreValidJson()
    {
        using var json = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "appsettings.Production.json")));
        Assert.Equal(System.Text.Json.JsonValueKind.Object, json.RootElement.ValueKind);
    }
}
