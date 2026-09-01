using CartService.Domain.Aggregates;
using CartService.Infrastructure;
using StackExchange.Redis;

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
        var repository = new CartServiceRepository(new RedisService(connection));
        var cart = CartAggregate.Create(Guid.NewGuid());
        cart.AddItem(Guid.NewGuid(), "Container product", 19.95m, 2);
        cart.Checkout();

        await repository.StoreCart(cart);
        var loaded = await repository.GetCart(cart.Id);

        Assert.NotNull(loaded);
        Assert.Equal(cart.Id, loaded.Id);
        Assert.Equal(39.90m, loaded.TotalPrice);
        Assert.True(loaded.IsCheckedOut);
        Assert.Equal("Container product", Assert.Single(loaded.Items).ProductName);
    }
}
