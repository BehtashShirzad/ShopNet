using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MediatR;
using Application.Abstractions.Contracts;
using Domain.Abstractions;
using Application.Abstractions;
using ShopNet.Contracts;

namespace CatalogService.Infrastructure
{
    public class DomainEventBus (IDomainEventDispatcher dispatcher): IDomainEventBus
    {
        public async Task PublishAsync<T>(T message, CancellationToken cancellationToken = default)
          where T : IDomainEvent
         
        {
        await   dispatcher.DispatchAsync(message, cancellationToken);
      
        }
 
    }
}
