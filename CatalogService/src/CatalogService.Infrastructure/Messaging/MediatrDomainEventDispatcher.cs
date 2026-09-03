using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Application.Abstractions;
using Domain.Abstractions;
using MediatR;

namespace CatalogService.Infrastructure
{
    public class MediatrDomainEventDispatcher : IDomainEventDispatcher
    { private readonly IPublisher _publisher;

    public MediatrDomainEventDispatcher(IPublisher publisher)
    {
        _publisher = publisher;
    }

    public async Task DispatchAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        var notificationType = typeof(DomainEventNotification<>).MakeGenericType(domainEvent.GetType());
        var notification = Activator.CreateInstance(notificationType, domainEvent);

        if (notification is INotification mediatrNotification)
        {
            await _publisher.Publish(mediatrNotification, cancellationToken);
        }
    }
    }
}
