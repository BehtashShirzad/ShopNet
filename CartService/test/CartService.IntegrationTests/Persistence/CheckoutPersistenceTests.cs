using System.Net;
using System.Net.Http.Json;
using CartService.Application.Checkout;
using CartService.Domain;
using CartService.Infrastructure;
using Grpc.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json;
using ShopNet.Contracts.IntegrationEvents;
using StackExchange.Redis;

namespace CartService.IntegrationTests;

[Collection(CartContainersCollection.Name)]
public sealed class CheckoutPersistenceTests(CartContainersFixture fixture)
{
    [Fact]
    public async Task CheckoutUsesInventoryNotLegacyCatalogStockAndAtomicallyPersistsSnapshot()
    {
        await using var host = await CheckoutTestHost.Create(fixture);
        var cart = await host.Seed();
        Assert.NotNull(await host.Redis.KeyTimeToLiveAsync(host.Keys.CartKey(cart.Id)));
        using var response = await host.Http.PostAsync($"/cart/checkout/{cart.Id}", null);
        response.EnsureSuccessStatusCode();
        Assert.Equal(cart.Id, await response.Content.ReadFromJsonAsync<Guid>());
        var saved = await host.Read(cart.Id);
        Assert.True(saved.IsCheckedOut);
        Assert.NotNull(saved.CheckoutEventId);
        Assert.Equal(1, await host.PendingCount());
        Assert.Null(await host.Redis.KeyTimeToLiveAsync(host.Keys.CartKey(cart.Id)));
        Assert.Null(await host.Redis.KeyTimeToLiveAsync(host.Keys.MessagesKey));
        Assert.Null(await host.Redis.KeyTimeToLiveAsync(host.Keys.PendingKey));
        var lease = await host.Outbox.ClaimAsync(default);
        var message = JsonConvert.DeserializeObject<CartCheckedOutEvent>(lease!.Payload)!;
        Assert.Equal(saved.CheckoutEventId, message.EventId);
        Assert.Equal(saved.CheckedOutAtUtc, message.OccurredOnUtc);
        Assert.Equal(20, message.TotalPrice);
        Assert.Equal(2, Assert.Single(message.Items).Quantity);
        Assert.Equal(1, host.Upstream.InventoryCalls);
        Assert.True(host.Upstream.InventoryDeadline < DateTime.UtcNow.AddSeconds(3));
        host.Upstream.CatalogFailure = "unavailable";
        host.Upstream.InventoryFailure = "unavailable";
        Assert.Equal(cart.Id, await host.Checkout(cart.Id));
        Assert.Equal(1, host.Upstream.CatalogCalls);
        Assert.Equal(1, host.Upstream.InventoryCalls);
        Assert.Equal(1, await host.PendingCount());
    }

    [Theory]
    [InlineData("price", HttpStatusCode.Conflict, "price_changed")]
    [InlineData("missing", HttpStatusCode.NotFound, "product_not_found")]
    [InlineData("short", HttpStatusCode.Conflict, "insufficient_stock")]
    [InlineData("inactive", HttpStatusCode.Conflict, "insufficient_stock")]
    [InlineData("unknown", HttpStatusCode.Conflict, "insufficient_stock")]
    [InlineData("catalog-offline", HttpStatusCode.ServiceUnavailable, "dependency_unavailable")]
    [InlineData("identity", HttpStatusCode.ServiceUnavailable, "dependency_unavailable")]
    [InlineData("unavailable", HttpStatusCode.ServiceUnavailable, "dependency_unavailable")]
    [InlineData("incomplete", HttpStatusCode.ServiceUnavailable, "dependency_unavailable")]
    [InlineData("duplicate", HttpStatusCode.ServiceUnavailable, "dependency_unavailable")]
    [InlineData("foreign", HttpStatusCode.ServiceUnavailable, "dependency_unavailable")]
    [InlineData("delay", HttpStatusCode.ServiceUnavailable, "dependency_unavailable")]
    public async Task RejectionOrUpstreamFailureLeavesCartEditableAndNoOutbox(string failure, HttpStatusCode status, string code)
    {
        await using var host = await CheckoutTestHost.Create(fixture);
        var cart = await host.Seed();
        switch (failure)
        {
            case "price": host.Upstream.Price = 11; break;
            case "missing": host.Upstream.CatalogFailure = "missing"; break;
            case "identity": host.Upstream.CatalogFailure = "identity"; break;
            case "catalog-offline": host.Upstream.CatalogFailure = "unavailable"; break;
            case "short": host.Upstream.Available = 1; break;
            case "inactive": host.Upstream.Active = false; break;
            case "unknown": host.Upstream.Exists = false; host.Upstream.Active = false; host.Upstream.Available = 0; break;
            default: host.Upstream.InventoryFailure = failure; break;
        }
        using var response = await host.Http.PostAsync($"/cart/checkout/{cart.Id}", null);
        Assert.Equal(status, response.StatusCode);
        Assert.Contains(code, await response.Content.ReadAsStringAsync());
        Assert.False((await host.Read(cart.Id)).IsCheckedOut);
        Assert.Equal(10, (await host.Read(cart.Id)).Items.Single().Price);
        Assert.Equal(0, await host.PendingCount());
    }

    [Fact]
    public async Task AuthorizationAndOwnershipAreStillEnforced()
    {
        await using var host = await CheckoutTestHost.Create(fixture);
        var cart = await host.Seed();
        using var anonymous = host.Http;
        anonymous.DefaultRequestHeaders.Add("X-Test-Anonymous", "true");
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsync($"/cart/checkout/{cart.Id}", null)).StatusCode);
        using var foreign = host.Http;
        foreign.DefaultRequestHeaders.Add("X-Test-User", Guid.NewGuid().ToString());
        Assert.Equal(HttpStatusCode.NotFound, (await foreign.PostAsync($"/cart/checkout/{cart.Id}", null)).StatusCode);
        Assert.Equal(0, host.Upstream.CatalogCalls);
        Assert.Equal(0, await host.PendingCount());
    }

    [Fact]
    public async Task ConcurrentCheckoutEnqueuesExactlyOneSnapshot()
    {
        await using var host = await CheckoutTestHost.Create(fixture);
        var cart = await host.Seed();
        var results = await Task.WhenAll(Enumerable.Range(0, 12).Select(async _ =>
        {
            try { return await host.Checkout(cart.Id) == cart.Id; }
            catch (CartConcurrencyException) { return false; }
        }));
        Assert.Contains(true, results);
        Assert.True((await host.Read(cart.Id)).IsCheckedOut);
        Assert.Equal(1, await host.PendingCount());
        Assert.Equal(1, await host.Redis.SortedSetLengthAsync(host.Keys.PendingKey));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StaleSaveCannotUndoCheckoutOrOverwriteNewerEdit(bool checkout)
    {
        await using var host = await CheckoutTestHost.Create(fixture);
        var cart = await host.Seed();
        await using var staleScope = host.Services.CreateAsyncScope();
        var staleRepo = staleScope.ServiceProvider.GetRequiredService<IRepository>();
        var stale = (await staleRepo.GetCart(cart.Id))!;
        if (checkout) await host.Checkout(cart.Id);
        else
        {
            await using var scope = host.Services.CreateAsyncScope();
            var repo = scope.ServiceProvider.GetRequiredService<IRepository>();
            var fresh = (await repo.GetCart(cart.Id))!;
            fresh.ChangeItemQuantity(host.Upstream.ProductId, 4);
            await repo.StoreCart(fresh);
        }
        stale.ChangeItemQuantity(host.Upstream.ProductId, 3);
        await Assert.ThrowsAsync<CartConcurrencyException>(() => staleRepo.StoreCart(stale));
        var saved = await host.Read(cart.Id);
        Assert.Equal(checkout, saved.IsCheckedOut);
        Assert.Equal(checkout ? 2 : 4, saved.Items.Single().Quantity);
    }

    [Fact]
    public async Task ScriptChecksAllKeyTypesBeforeAnyWrite()
    {
        await using var host = await CheckoutTestHost.Create(fixture);
        var cart = await host.Seed();
        await host.Redis.StringSetAsync(host.Keys.PendingKey, "wrong-type");
        await Assert.ThrowsAsync<RedisServerException>(() => host.Checkout(cart.Id));
        Assert.False((await host.Read(cart.Id)).IsCheckedOut);
        Assert.Equal(0, await host.PendingCount());
        Assert.Equal("wrong-type", await host.Redis.StringGetAsync(host.Keys.PendingKey));
    }

    [Fact]
    public async Task MissingCartCannotBeRecreatedByStaleSave()
    {
        await using var host = await CheckoutTestHost.Create(fixture);
        var cart = await host.Seed();
        await using var scope = host.Services.CreateAsyncScope();
        var repo = scope.ServiceProvider.GetRequiredService<IRepository>();
        var stale = (await repo.GetCart(cart.Id))!;
        // Simulate TTL expiry on this test's unique key, without waiting 24 hours.
        await host.Redis.KeyDeleteAsync(host.Keys.CartKey(cart.Id));
        await Assert.ThrowsAsync<CartConcurrencyException>(() => repo.StoreCart(stale));
        Assert.False(await host.Redis.KeyExistsAsync(host.Keys.CartKey(cart.Id)));
    }

    [Fact]
    public async Task LeaseIsExclusiveAndExpiredOwnerCannotAcknowledgeNewDelivery()
    {
        await using var host = await CheckoutTestHost.Create(fixture);
        var cart = await host.Seed();
        await host.Checkout(cart.Id);
        var first = (await host.Outbox.ClaimAsync(default))!;
        Assert.Null(await host.Outbox.ClaimAsync(default));
        await host.Redis.SortedSetAddAsync(host.Keys.PendingKey, first.EventId.ToString("N"), 0);
        var second = (await host.Outbox.ClaimAsync(default))!;
        Assert.Equal(first.EventId, second.EventId);
        Assert.Equal(first.Payload, second.Payload);
        Assert.NotEqual(first.Token, second.Token);
        Assert.False(await host.Outbox.AcknowledgeAsync(first, default));
        await host.Outbox.RetryAsync(first, default);
        Assert.Null(await host.Outbox.ClaimAsync(default));
        Assert.Equal(1, await host.PendingCount());
        Assert.True(await host.Outbox.AcknowledgeAsync(second, default));
        Assert.Equal(0, await host.PendingCount());
        Assert.Equal(0, await host.Redis.SortedSetLengthAsync(host.Keys.PendingKey));
        Assert.Equal(0, await host.Redis.HashLengthAsync(host.Keys.LeasesKey));
        Assert.True((await host.Read(cart.Id)).IsCheckedOut);
    }

    [Fact]
    public async Task PublishingFailureRetainsActualRedisOutboxAndSchedulesRetry()
    {
        await using var host = await CheckoutTestHost.Create(fixture);
        var cart = await host.Seed();
        await host.Checkout(cart.Id);
        var dispatcher = new CartOutboxDispatcher(host.Outbox, new FailingPublisher(), NullLogger<CartOutboxDispatcher>.Instance);
        Assert.Equal(0, await dispatcher.RunOnceAsync(default));
        Assert.Equal(1, await host.PendingCount());
        Assert.Null(await host.Outbox.ClaimAsync(default));
        var saved = await host.Read(cart.Id);
        await host.Redis.SortedSetAddAsync(host.Keys.PendingKey, saved.CheckoutEventId!.Value.ToString("N"), 0);
        Assert.Equal(saved.CheckoutEventId, (await host.Outbox.ClaimAsync(default))!.EventId);
    }

    [Fact]
    public async Task CancellationReachesGrpcWithoutCheckoutMutation()
    {
        await using var host = await CheckoutTestHost.Create(fixture);
        host.Upstream.InventoryFailure = "delay";
        await using var scope = host.Services.CreateAsyncScope();
        using var cancellation = new CancellationTokenSource();
        var task = scope.ServiceProvider.GetRequiredService<IInventoryAvailabilityClient>()
            .GetAvailabilityAsync([host.Upstream.ProductId], cancellation.Token);
        await host.Upstream.InventoryEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();
        var error = await Assert.ThrowsAsync<RpcException>(() => task);
        Assert.Equal(StatusCode.Cancelled, error.StatusCode);
        Assert.Equal(0, await host.PendingCount());
    }

    private sealed class FailingPublisher : ICheckoutPublisher
    {
        public Task PublishAsync(CartCheckedOutEvent message, CancellationToken ct)
            => Task.FromException(new IOException("Simulated disconnect after Redis commit"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LegacyJsonRemainsReadableWithoutInventingPastCheckoutEvents(bool closed)
    {
        await using var host = await CheckoutTestHost.Create(fixture);
        var id = Guid.NewGuid();
        var legacy = JsonConvert.SerializeObject(new
        {
            Id = id, CustomerId = CheckoutTestHost.Customer, IsCheckedOut = closed,
            Items = new[] { new { ProductId = host.Upstream.ProductId, ProductName = "Product", Price = 10m, Quantity = 2 } }
        });
        await host.Redis.StringSetAsync(host.Keys.CartKey(id), legacy, TimeSpan.FromHours(24));
        var read = await host.Read(id);
        Assert.Equal(20, read.TotalPrice);
        Assert.Null(read.CheckoutEventId);
        Assert.Equal(id, await host.Checkout(id));
        Assert.Equal(closed ? 0 : 1, await host.PendingCount());
        Assert.Equal(closed, (await host.Read(id)).CheckoutEventId is null);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(-1)]
    [InlineData(0.001)]
    public async Task InvalidCatalogPricesFailClosed(double price)
    {
        await using var host = await CheckoutTestHost.Create(fixture);
        var cart = await host.Seed();
        host.Upstream.Price = price;
        var error = await Assert.ThrowsAsync<RpcException>(() => host.Checkout(cart.Id));
        Assert.Equal(StatusCode.DataLoss, error.StatusCode);
        Assert.False((await host.Read(cart.Id)).IsCheckedOut);
        Assert.Equal(0, await host.PendingCount());
    }

    [Fact]
    public async Task InventoryBatchDeduplicatesRequestIdsAndRejectsImpossibleState()
    {
        await using var host = await CheckoutTestHost.Create(fixture);
        await using var scope = host.Services.CreateAsyncScope();
        var client = scope.ServiceProvider.GetRequiredService<IInventoryAvailabilityClient>();
        var ids = new[] { host.Upstream.ProductId, Guid.NewGuid(), host.Upstream.ProductId };
        var response = await client.GetAvailabilityAsync(ids, default);
        Assert.Equal(2, response.Count);
        Assert.Equal(1, host.Upstream.InventoryCalls);
        host.Upstream.Available = -1;
        Assert.Equal(StatusCode.DataLoss, (await Assert.ThrowsAsync<RpcException>(() => client.GetAvailabilityAsync(ids, default))).StatusCode);
        host.Upstream.Available = 1;
        host.Upstream.Exists = false;
        Assert.Equal(StatusCode.DataLoss, (await Assert.ThrowsAsync<RpcException>(() => client.GetAvailabilityAsync(ids, default))).StatusCode);
    }

    [Fact]
    public async Task InvalidInventoryRequestsNeverReachGrpc()
    {
        await using var host = await CheckoutTestHost.Create(fixture);
        await using var scope = host.Services.CreateAsyncScope();
        var client = scope.ServiceProvider.GetRequiredService<IInventoryAvailabilityClient>();
        foreach (var ids in new[] { Array.Empty<Guid>(), new[] { Guid.Empty }, Enumerable.Range(0, 101).Select(_ => Guid.NewGuid()).ToArray() })
            await Assert.ThrowsAsync<ArgumentException>(() => client.GetAvailabilityAsync(ids, default));
        Assert.Equal(0, host.Upstream.InventoryCalls);
    }

    [Fact]
    public async Task CheckedOutCartCannotBeEditedThroughHttp()
    {
        await using var host = await CheckoutTestHost.Create(fixture);
        var cart = await host.Seed();
        await host.Checkout(cart.Id);
        using var response = await host.Http.PutAsJsonAsync($"/cart/items/{cart.Id}",
            new CartService.Application.Commands.ProductViewModelInput(host.Upstream.ProductId, 1));
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("cart_closed", await response.Content.ReadAsStringAsync());
        Assert.Equal(2, (await host.Read(cart.Id)).Items.Single().Quantity);
        Assert.Equal(1, await host.PendingCount());
    }
}
