using MassTransit;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using OrderService.Application.Inventory;
using OrderService.Domain.Enums;
using ShopNet.Contracts.Inventory.V1;

namespace OrderService.Application.IntegrationEventHandler;

public sealed class InventoryResultConsumer(IServiceScopeFactory scopes) :
    IConsumer<InventoryReserved>, IConsumer<InventoryRejected>, IConsumer<InventoryReleased>,
    IConsumer<InventoryExpired>, IConsumer<InventoryCommitted>, IConsumer<InventoryCommandRejected>
{
    private async Task Send<T>(T command, CancellationToken ct) where T : MediatR.IRequest
    {
        await using var scope = scopes.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ISender>().Send(command, ct);
    }

    public Task Consume(ConsumeContext<InventoryReserved> context) => Send(new ApplyInventoryResultCommand(
        context.Message.OrderId, context.Message.ReservationRequestId, context.Message.ReservationVersion,
        OrderInventoryStatus.Reserved, Items: context.Message.Items, ExpiresAtUtc: context.Message.ExpiresAtUtc),
        context.CancellationToken);
    public Task Consume(ConsumeContext<InventoryRejected> context) => Send(new ApplyInventoryResultCommand(
        context.Message.OrderId, context.Message.ReservationRequestId, context.Message.ReservationVersion,
        OrderInventoryStatus.Rejected, context.Message.Reason), context.CancellationToken);
    public Task Consume(ConsumeContext<InventoryReleased> context) => Send(new ApplyInventoryResultCommand(
        context.Message.OrderId, context.Message.ReservationRequestId, context.Message.ReservationVersion,
        OrderInventoryStatus.Released, "InventoryReleased"), context.CancellationToken);
    public Task Consume(ConsumeContext<InventoryExpired> context) => Send(new ApplyInventoryResultCommand(
        context.Message.OrderId, context.Message.ReservationRequestId, context.Message.ReservationVersion,
        OrderInventoryStatus.Expired, "ReservationExpired"), context.CancellationToken);
    public Task Consume(ConsumeContext<InventoryCommitted> context) => Send(new ApplyInventoryResultCommand(
        context.Message.OrderId, context.Message.ReservationRequestId, context.Message.ReservationVersion,
        OrderInventoryStatus.Committed, "UnexpectedInventoryCommit"), context.CancellationToken);
    public Task Consume(ConsumeContext<InventoryCommandRejected> context) => Send(new InventoryCommandRejectedCommand(
        context.Message.OrderId, context.Message.ReservationRequestId, context.Message.Operation,
        context.Message.Reason), context.CancellationToken);
}
