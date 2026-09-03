using CartService.Domain.Aggregates;
using ShopNet.Contracts.IntegrationEvents;

namespace CartService.Application.Checkout;

public sealed record InventoryAvailability(Guid ProductId, bool Exists, bool IsActive, int AvailableQuantity);
public interface IInventoryAvailabilityClient
{
    Task<IReadOnlyDictionary<Guid, InventoryAvailability>> GetAvailabilityAsync(Guid[] productIds, CancellationToken cancellationToken);
}
public interface ICartCheckoutStore
{
    Task CompleteAsync(CartAggregate cart, CartCheckedOutEvent message, CancellationToken cancellationToken);
}
public sealed class CheckoutRejectedException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
public sealed class CartConcurrencyException() : Exception("The cart changed concurrently. Reload it before retrying.");
