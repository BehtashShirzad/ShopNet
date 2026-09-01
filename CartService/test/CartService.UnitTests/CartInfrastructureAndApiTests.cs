using System.Security.Claims;
using CartService.Api;
using CartService.Domain.Aggregates;
using CartService.Infrastructure;
using Microsoft.AspNetCore.Http;

namespace CartService.UnitTests;

public class CartInfrastructureAndApiTests
{
    [Fact]
    public async Task Repository_RoundTripsSerializedCart()
    {
        var redis = new InMemoryRedisService();
        var repository = new CartServiceRepository(redis);
        var cart = CartAggregate.Create(Guid.NewGuid());
        cart.AddItem(Guid.NewGuid(), "Product", 12m, 2);

        await repository.StoreCart(cart);
        var loaded = await repository.GetCart(cart.Id);

        Assert.NotNull(loaded);
        Assert.Equal(cart.Id, loaded.Id);
        Assert.Equal(cart.CustomerId, loaded.CustomerId);
        Assert.Equal(24m, loaded.TotalPrice);
        Assert.Equal("Product", Assert.Single(loaded.Items).ProductName);
    }

    [Fact]
    public async Task Repository_ReturnsNullForMissingCart()
    {
        var repository = new CartServiceRepository(new InMemoryRedisService());

        Assert.Null(await repository.GetCart(Guid.NewGuid()));
    }

    [Fact]
    public void ContextHelper_ReadsSubjectClaim()
    {
        var userId = Guid.NewGuid();
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([
                new Claim("sub", userId.ToString())
            ]))
        };
        IHttpContextAccessor accessor = new HttpContextAccessor { HttpContext = context };

        Assert.Equal(userId, accessor.GetUserId());
    }

    [Fact]
    public void ContextHelper_UsesDevelopmentFallbackWithoutSubject()
    {
        IHttpContextAccessor accessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext()
        };

        Assert.Equal(Guid.Parse("00000000-0000-0000-0000-000000000011"), accessor.GetUserId());
    }

    private sealed class InMemoryRedisService : IRedisService
    {
        private readonly Dictionary<string, string> _values = [];

        public Task<string?> GetValue(string key) =>
            Task.FromResult(_values.GetValueOrDefault(key));

        public Task SetValue(string key, string value, TimeSpan? expiry = null)
        {
            _values[key] = value;
            return Task.CompletedTask;
        }
    }
}
