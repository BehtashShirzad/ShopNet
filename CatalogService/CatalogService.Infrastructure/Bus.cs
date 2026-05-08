using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MediatR;
using Application.Abstractions.Contracts;
using Domain.Abstractions;

namespace CatalogService.Infrastructure
{
    public class Bus(IMediator mediator): IBus
    {
        public async Task PublishAsync<T>(T message, CancellationToken cancellationToken = default)
          where T : IDomainEvent
         
        {
        await   mediator.Publish(message, cancellationToken);
      
        }

        public Task PublishIntegrationMessage<T>(T message, CancellationToken cancellationToken = default)
         where T : class
        {
            throw new NotImplementedException();
        }
    }
}