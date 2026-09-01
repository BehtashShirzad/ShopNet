using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShopNet.Contracts.Interfaces;

namespace Application.Abstractions.Contracts
{
   public interface IIntegrationEventBus
{
    Task PublishAsync<T>(
        T integrationEvent,
        CancellationToken cancellationToken = default)
        where T : IIntegrationEvent;
}

}