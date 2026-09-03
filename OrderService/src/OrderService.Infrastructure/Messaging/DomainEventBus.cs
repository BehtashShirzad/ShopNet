using Application.Abstractions;
using Application.Abstractions.Contracts;
using Domain.Abstractions;

namespace OrderService.Infrastructure;

public class DomainEventBus (IDomainEventDispatcher dispatcher): IDomainEventBus
{
    public async Task PublishAsync<T>(T message, CancellationToken cancellationToken = default)
        where T : IDomainEvent
         
    {
        await   dispatcher.DispatchAsync(message, cancellationToken);
      
    }
 
}
