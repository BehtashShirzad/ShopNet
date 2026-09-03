using CartService.Domain.Aggregates;
using CartService.Infrastructure;
using StackExchange.Redis;
using ShopNet.Contracts.IntegrationEvents;
using ShopNet.Contracts.SharedDtos;

namespace CartService.IntegrationTests;

[Collection(CartContainersCollection.Name)]
public class RedisIntegrationTests(CartContainersFixture fixture)
{
    [Fact]
    public async Task RedisService_ReadsAndWritesAgainstContainer()
    {
        await using var connection = await ConnectionMultiplexer.ConnectAsync(
            fixture.RedisConnectionString);
        var service = new RedisService(connection);
        var key = $"integration:{Guid.NewGuid():N}";

        await service.SetValue(key, "container-value", TimeSpan.FromMinutes(1));

        Assert.Equal("container-value", await service.GetValue(key));
    }

    [Fact]
    public async Task CartRepository_RoundTripsAggregateThroughRedisContainer()
    {
        await using var connection = await ConnectionMultiplexer.ConnectAsync(
            fixture.RedisConnectionString);
        var repository = new CartServiceRepository(new CartRedisPersistence(connection, new CartRedisOptions { KeyPrefix = Guid.NewGuid().ToString() }));
        var cart = CartAggregate.Create(Guid.NewGuid());
        cart.AddItem(Guid.NewGuid(), "Container product", 19.95m, 2);
        await repository.StoreCart(cart);
        cart.Checkout();
        await repository.CompleteAsync(cart, new CartCheckedOutEvent(cart.Id, cart.CustomerId,
            cart.Items.Select(x => new ProductDto(x.ProductId, x.ProductName, x.Price, x.Quantity)).ToList(), cart.TotalPrice)
            { EventId = cart.CheckoutEventId!.Value, OccurredOnUtc = cart.CheckedOutAtUtc!.Value }, default);
        var loaded = await repository.GetCart(cart.Id);

        Assert.NotNull(loaded);
        Assert.Equal(cart.Id, loaded.Id);
        Assert.Equal(39.90m, loaded.TotalPrice);
        Assert.True(loaded.IsCheckedOut);
        Assert.Equal("Container product", Assert.Single(loaded.Items).ProductName);
    }
}
