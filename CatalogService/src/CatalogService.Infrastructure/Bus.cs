using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Application.Abstractions.Contracts;
using MassTransit;
using ShopNet.Contracts.Interfaces;

namespace CatalogService.Infrastructure
{
    public class Bus(IBus bus): IIntegrationEventBus
    {
        public async Task PublishAsync<T>(T integrationEvent, CancellationToken cancellationToken = default) where T : IIntegrationEvent
        {
            await bus.Publish(integrationEvent,cancellationToken);
           
        }
    }
}