using MassTransit;
using Microsoft.Extensions.Configuration;
using OrderService.Application.Inventory;
using ShopNet.Contracts.Inventory.V1;

namespace OrderService.Infrastructure;

public sealed class InventoryCommandSender(ISendEndpointProvider endpoints, IConfiguration configuration) : IInventoryCommandSender
{
    public async Task ReserveAsync(ReserveInventory command, CancellationToken cancellationToken)
    {
        var destination = configuration["Inventory:CommandQueue"] ?? InventoryQueues.Commands;
        var endpoint = await endpoints.GetSendEndpoint(new Uri("queue:" + destination));
        await endpoint.Send(command, context =>
        {
            context.MessageId = command.ReservationRequestId;
            context.CorrelationId = command.OrderId;
        }, cancellationToken);
    }
}
