using Application.Abstractions.Contracts;
using MassTransit;
using ShopNet.Contracts.Interfaces;

namespace CartService.Infrastructure;

public class Bus(IBus bus): IIntegrationEventBus
{
    public async Task PublishAsync<T>(T integrationEvent, CancellationToken cancellationToken = default) where T : IIntegrationEvent
    {
        await bus.Publish(integrationEvent,cancellationToken);
           
    }
}