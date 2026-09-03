using Application.Abstractions.Contracts;
using OrderService.Domain;
using OrderService.Domain.Enums;
using ShopNet.Contracts.Inventory.V1;

namespace OrderService.Application.Inventory;

public sealed record ApplyInventoryResultCommand(Guid OrderId, Guid ReservationRequestId,
    long Version, OrderInventoryStatus Result, string? Reason = null,
    InventoryLine[]? Items = null, DateTimeOffset? ExpiresAtUtc = null) : ICommand;

public sealed class ApplyInventoryResultCommandHandler(IOrderRepository repository, IOrderTransactionLock transactionLock)
    : ICommandHandler<ApplyInventoryResultCommand>
{
    public async Task Handle(ApplyInventoryResultCommand command, CancellationToken ct)
    {
        if (command.OrderId == Guid.Empty || command.ReservationRequestId == Guid.Empty)
            throw new ArgumentException("Order and reservation IDs are required.");
        if (command.Version <= 0 || !Enum.IsDefined(command.Result) || command.Result == OrderInventoryStatus.Requested)
            throw new ArgumentException("Invalid inventory result/version.");
        await transactionLock.AcquireAsync($"order:{command.OrderId:N}", ct);
        var order = await repository.GetByIdAsync(command.OrderId);
        // An unknown order cannot legitimately have been reserved by this Order service.
        // Fault it for investigation/replay rather than silently acknowledge a possibly misrouted result.
        if (order is null) throw new InvalidOperationException("Inventory result references an unknown order.");
        if (order.InventoryReservationRequestId != command.ReservationRequestId ||
            command.Version <= order.InventoryReservationVersion) return;
        if (command.Result == OrderInventoryStatus.Reserved)
        {
            var expected = order.Items.OrderBy(x => x.ProductId).Select(x => new InventoryLine(x.ProductId, x.Quantity));
            if (command.Items is null || command.Items.Any(x => x is null) ||
                !expected.SequenceEqual(command.Items.OrderBy(x => x.ProductId)) ||
                command.ExpiresAtUtc != order.InventoryReservationExpiresAtUtc)
                throw new ArgumentException("Inventory reserved lines/deadline do not match the requested reservation.");
        }
        order.ApplyInventoryResult(command.ReservationRequestId, command.Version, command.Result, command.Reason);
    }
}

public sealed record InventoryCommandRejectedCommand(Guid OrderId, Guid ReservationRequestId,
    string Operation, string Reason) : ICommand;

public sealed class InventoryCommandRejectedCommandHandler(IOrderRepository repository, IOrderTransactionLock transactionLock)
    : ICommandHandler<InventoryCommandRejectedCommand>
{
    public async Task Handle(InventoryCommandRejectedCommand command, CancellationToken ct)
    {
        if (command.OrderId == Guid.Empty || command.ReservationRequestId == Guid.Empty)
            throw new ArgumentException("Order and reservation IDs are required.");
        await transactionLock.AcquireAsync($"order:{command.OrderId:N}", ct);
        var order = await repository.GetByIdAsync(command.OrderId)
            ?? throw new InvalidOperationException("Inventory rejection references an unknown order.");
        order.FlagInventoryCommandRejection(command.ReservationRequestId, command.Operation, command.Reason);
    }
}
