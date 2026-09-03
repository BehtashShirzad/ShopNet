using Application.Abstractions.Contracts;
using OrderService.Application.Inventory;
using OrderService.Domain;
using OrderService.Domain.Aggregates;
using OrderService.Domain.DomanEvents;
using ShopNet.Contracts.Inventory.V1;
using ShopNet.Contracts.SharedDtos;

namespace OrderService.Application.Commands;

public record CreateOrderCommand(Guid CartId, Guid CustomerId, List<ProductDto> Items, decimal TotalPrice) : ICommand;

public sealed class CreateOrderCommandHandler(IOrderRepository repository, IInventoryCommandSender inventory,
    IOrderTransactionLock transactionLock, TimeProvider clock, OrderInventoryOptions options)
    : ICommandHandler<CreateOrderCommand>
{
    public async Task Handle(CreateOrderCommand request, CancellationToken cancellationToken)
    {
        // Validate before locking or enqueueing anything. Cart still uses the legacy checkout contract.
        var candidate = OrderAggregate.Create(request.CustomerId, request.CartId);
        if (request.Items is null || request.Items.Count is 0 or > 100 || request.Items.Any(x => x is null))
            throw new ArgumentException("A checkout must contain 1-100 products.");
        foreach (var item in request.Items)
            candidate.AddItem(item.ProductId, item.ProductName, item.Price, item.Quantity);
        if (candidate.TotalPrice != request.TotalPrice)
            throw new ArgumentException("Checkout total does not match its line items.");

        await transactionLock.AcquireAsync($"cart:{request.CartId:N}", cancellationToken);
        var existing = await repository.GetByCartId(request.CartId);
        if (existing is not null)
        {
            if (existing.CustomerId != candidate.CustomerId ||
                !existing.Items.OrderBy(x => x.ProductId).SequenceEqual(candidate.Items.OrderBy(x => x.ProductId)))
                throw new ArgumentException("CartId was reused with a different checkout.");
            // Completed/legacy orders are not silently re-reserved; an exact retry is a no-op.
            return;
        }

        options.Validate();
        candidate.BeginInventoryReservation(Guid.NewGuid(), clock.GetUtcNow(), options.ReservationDuration);
        candidate.RaiseEvent(new OrderCreatedDomainEvent(candidate.Id, candidate.Items.ToList(), candidate.CustomerId));
        await repository.AddAsync(candidate);
        await inventory.ReserveAsync(new ReserveInventory(candidate.Id, candidate.InventoryReservationRequestId!.Value,
            candidate.Items.OrderBy(x => x.ProductId).Select(x => new InventoryLine(x.ProductId, x.Quantity)).ToArray(),
            candidate.InventoryReservationExpiresAtUtc!.Value), cancellationToken);
    }
}
