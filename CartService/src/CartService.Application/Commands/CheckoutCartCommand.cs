using Application.Abstractions.Contracts;
using CartService.Application.Checkout;
using CartService.Domain;
using ShopNet.Contracts.IntegrationEvents;
using ShopNet.Contracts.SharedDtos;

namespace CartService.Application.Commands;

public record CheckoutCartCommand(Guid CartId, Guid UserId) : ICommand<Guid>;

public sealed class CheckoutCartCommandHandler(IRepository repository, ICatalogService catalog,
    IInventoryAvailabilityClient inventory, ICartCheckoutStore checkoutStore, TimeProvider clock)
    : ICommandHandler<CheckoutCartCommand, Guid>
{
    public async Task<Guid> Handle(CheckoutCartCommand request, CancellationToken ct)
    {
        var cart = await repository.GetCart(request.CartId);
        if (cart is null || cart.CustomerId != request.UserId)
            throw new CheckoutRejectedException("cart_not_found", "Cart not found");
        // A successful retry does not depend on current stock/prices or create another message.
        if (cart.IsCheckedOut) return cart.Id;
        if (cart.Items.Count == 0)
            throw new CheckoutRejectedException("empty_cart", "Cannot checkout an empty cart.");
        foreach (var item in cart.Items)
        {
            var product = await catalog.GetProduct(item.ProductId, ct);
            if (product is null) throw new CheckoutRejectedException("product_not_found", $"Product {item.ProductId} was not found.");
            if (product.Id != item.ProductId)
                throw new CheckoutRejectedException("invalid_catalog_response", "Catalog returned a different product.");
            if (product.Price != item.Price)
                throw new CheckoutRejectedException("price_changed",
                    "A product price changed. Create/review a new cart before checking out.");
        }
        var availability = await inventory.GetAvailabilityAsync(cart.Items.Select(x => x.ProductId).ToArray(), ct);
        foreach (var item in cart.Items)
            if (!availability.TryGetValue(item.ProductId, out var stock) || !stock.Exists || !stock.IsActive ||
                stock.AvailableQuantity < item.Quantity)
                throw new CheckoutRejectedException("insufficient_stock", $"Product {item.ProductId} is out of stock.");

        cart.Checkout(Guid.NewGuid(), clock.GetUtcNow());
        var message = new CartCheckedOutEvent(cart.Id, cart.CustomerId,
            cart.Items.Select(x => new ProductDto(x.ProductId, x.ProductName, x.Price, x.Quantity)).ToList(), cart.TotalPrice)
        {
            EventId = cart.CheckoutEventId!.Value,
            OccurredOnUtc = cart.CheckedOutAtUtc!.Value
        };
        // This operation atomically persists the immutable checkout and the pending Redis outbox message.
        await checkoutStore.CompleteAsync(cart, message, ct);
        return cart.Id;
    }
}
