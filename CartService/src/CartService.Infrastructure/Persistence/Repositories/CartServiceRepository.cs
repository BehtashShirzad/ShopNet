using CartService.Application.Checkout;
using CartService.Domain;
using CartService.Domain.Aggregates;
using Newtonsoft.Json;
using ShopNet.Contracts.IntegrationEvents;

namespace CartService.Infrastructure;

public sealed class CartServiceRepository(ICartRedisPersistence storage) : IRepository, ICartCheckoutStore
{
    private readonly Dictionary<Guid, string> _snapshots = [];
    private static readonly JsonSerializerSettings Settings = new()
    {
        ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor,
        TypeNameHandling = TypeNameHandling.None
    };
    public async Task<CartAggregate?> GetCart(Guid cartId)
    {
        var json = await storage.ReadAsync(cartId);
        if (json is null) { _snapshots.Remove(cartId); return null; }
        var cart = JsonConvert.DeserializeObject<CartAggregate>(json, Settings)
            ?? throw new InvalidOperationException("Invalid stored cart.");
        if (cart.Id != cartId) throw new InvalidOperationException("Stored cart identity mismatch.");
        _snapshots[cartId] = json;
        return cart;
    }
    public async Task StoreCart(CartAggregate cart)
    {
        if (cart.IsCheckedOut) throw new InvalidOperationException("Checkout requires the atomic checkout/outbox operation.");
        var json = JsonConvert.SerializeObject(cart);
        if (!await storage.SaveAsync(cart.Id, _snapshots.GetValueOrDefault(cart.Id), json))
            throw new CartConcurrencyException();
        _snapshots[cart.Id] = json;
    }
    public async Task CompleteAsync(CartAggregate cart, CartCheckedOutEvent message, CancellationToken ct)
    {
        if (!cart.IsCheckedOut || cart.CheckoutEventId != message.EventId || message.CartId != cart.Id ||
            message.CustomerId != cart.CustomerId || message.TotalPrice != cart.TotalPrice ||
            message.OccurredOnUtc != cart.CheckedOutAtUtc || message.Items is null ||
            !cart.Items.Select(x => new ShopNet.Contracts.SharedDtos.ProductDto(x.ProductId, x.ProductName, x.Price, x.Quantity))
                .SequenceEqual(message.Items))
            throw new InvalidOperationException("Checkout payload does not match the immutable cart snapshot.");
        if (!_snapshots.TryGetValue(cart.Id, out var expected)) throw new CartConcurrencyException();
        var json = JsonConvert.SerializeObject(cart);
        if (!await storage.CheckoutAsync(cart.Id, expected, json, message.EventId, JsonConvert.SerializeObject(message), ct))
            throw new CartConcurrencyException();
        _snapshots[cart.Id] = json;
    }
}
