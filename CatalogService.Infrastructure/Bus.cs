using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MediatR;
using Application.Abstractions.Contracts;

namespace CatalogService.Infrastructure
{
    public class Bus(IMediator mediator): IBus
    {
        public async Task PublishAsync<T>(T message, CancellationToken cancellationToken = default) where T : class
        {
        await   mediator.Publish(message, cancellationToken);
      
        }

        public Task PublishIntegratedMessage<T>(T message, CancellationToken cancellationToken = default) where T : class
        {
            throw new NotImplementedException();
        }
    }
}