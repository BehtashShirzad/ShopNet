using MassTransit;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using OrderService.Application.Commands;
using ShopNet.Contracts.IntegrationEvents;

namespace OrderService.Application.IntegrationEventHandler;

// A fresh application scope keeps the pipeline's SQL transaction and BusOutbox together,
// without inheriting the consumer's publishing endpoint. Ack only after Send/commit succeeds.
public sealed class CartCheckedOutEventHandler(IServiceScopeFactory scopes) : IConsumer<CartCheckedOutEvent>
{
    public async Task Consume(ConsumeContext<CartCheckedOutEvent> context)
    {
        await using var scope = scopes.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ISender>().Send(
            new CreateOrderCommand(context.Message.CartId, context.Message.CustomerId,
                context.Message.Items, context.Message.TotalPrice), context.CancellationToken);
    }
}
