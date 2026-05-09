using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Application.Abstractions.Contracts;
using ShopNet.Contracts.Interfaces;

namespace CatalogService.Infrastructure
{
    public class IntegrationEventBus : IIntegrationEventBus
    {
        public Task PublishAsync<T>(T integrationEvent, CancellationToken cancellationToken = default) where T : IIntegrationEvent
        {
            throw new NotImplementedException();
        }
    }
}