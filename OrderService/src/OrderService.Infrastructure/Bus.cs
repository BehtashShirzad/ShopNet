using Application.Abstractions.Contracts;
using MassTransit;
using ShopNet.Contracts.Interfaces;

namespace OrderService.Infrastructure;

public sealed class Bus(IPublishEndpoint endpoint) : IIntegrationEventBus
{
    public Task PublishAsync<T>(T integrationEvent, CancellationToken cancellationToken = default)
        where T : IIntegrationEvent
        => endpoint.Publish((object)integrationEvent, cancellationToken);
}
