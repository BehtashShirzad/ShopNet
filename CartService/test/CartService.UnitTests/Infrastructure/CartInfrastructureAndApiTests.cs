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
        var redis = new InMemoryCartPersistence();
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
        var repository = new CartServiceRepository(new InMemoryCartPersistence());

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

    private sealed class InMemoryCartPersistence : ICartRedisPersistence
    {
        private readonly Dictionary<string, string> _values = [];

        public Task<string?> ReadAsync(Guid id) =>
            Task.FromResult(_values.GetValueOrDefault(id.ToString()));

        public Task<bool> SaveAsync(Guid id, string? expected, string value)
        {
            if (_values.GetValueOrDefault(id.ToString()) != expected) return Task.FromResult(false);
            _values[id.ToString()] = value;
            return Task.FromResult(true);
        }
        public Task<bool> CheckoutAsync(Guid id, string expected, string value, Guid eventId, string message, CancellationToken ct)
            => throw new NotSupportedException();
    }
}
