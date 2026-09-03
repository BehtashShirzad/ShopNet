using InventoryService.Application;
using MassTransit;
using ShopNet.Contracts;

namespace InventoryService.Infrastructure;

public sealed class InventoryEventPublisher(IPublishEndpoint endpoint) : IInventoryEventPublisher
{
    public Task PublishAsync(IntegrationEvent message, CancellationToken cancellationToken)
        => endpoint.Publish((object)message, context => context.MessageId = message.EventId, cancellationToken);
}
