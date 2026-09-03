using Application.Abstractions.Contracts;
using MassTransit;
using ShopNet.Contracts.Interfaces;

namespace CatalogService.Infrastructure;

// The scoped endpoint is intercepted by the EF bus outbox.
// Injecting IBus here would bypass that transaction.
public sealed class Bus(IPublishEndpoint publishEndpoint) : IIntegrationEventBus
{
    public Task PublishAsync<T>(
        T integrationEvent,
        CancellationToken cancellationToken = default)
        where T : IIntegrationEvent
    {
        return publishEndpoint.Publish(integrationEvent, cancellationToken);
    }
}
