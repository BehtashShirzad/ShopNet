using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Domain.Abstractions;

namespace Application.Abstractions.Contracts
{
   public interface IBus
{
    Task PublishAsync<T>(T message, CancellationToken cancellationToken = default)
        where T : IDomainEvent;

        Task PublishIntegrationMessage<T>(T message, CancellationToken cancellationToken = default)
        where T : class;
}

}