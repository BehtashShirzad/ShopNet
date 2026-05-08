using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Application.Abstractions;
using CatalogService.Domain.DomainEvents;
using MediatR;

namespace CatalogService.Application.Features.Product.DomainEventHandlers
{
    public class ProductCreatedDomainEventHandler 
    : INotificationHandler<DomainEventNotification<ProductCreatedDomainEvent>>
    {
  

        public Task Handle(DomainEventNotification<ProductCreatedDomainEvent> notification, 
        CancellationToken cancellationToken)
        {
         var domainEvent = notification.DomainEvent;

        // logic here

        return Task.CompletedTask;
        }
    }
}