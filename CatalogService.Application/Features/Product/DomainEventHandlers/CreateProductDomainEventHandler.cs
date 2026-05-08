using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CatalogService.Domain.DomainEvents;
using MediatR;

namespace CatalogService.Application.Features.Product.DomainEventHandlers
{
    public class CreateProductDomainEventHandler : INotificationHandler<ProductCreatedDomainEvent>
    {
        public Task Handle(ProductCreatedDomainEvent notification, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}