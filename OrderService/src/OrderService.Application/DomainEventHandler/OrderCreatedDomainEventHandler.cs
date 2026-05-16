using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Application.Abstractions;
using Application.Abstractions.Contracts;
using MediatR;
using OrderService.Domain.DomanEvents;
using ShopNet.Contracts.IntegrationEvents;
using ShopNet.Contracts.SharedDtos;

namespace OrderService.Application.DomainEventHandler;

    public class OrderCreatedDomainEventHandler(IIntegrationEventBus integrationEventBus)
    : INotificationHandler<DomainEventNotification<OrderCreatedDomainEvent>>
    {
      

        public async Task Handle(DomainEventNotification<OrderCreatedDomainEvent> notification, CancellationToken cancellationToken)
        {
            
            var items = notification.DomainEvent.OrderItems
                .Select(x => new ProductDto(
                    x.ProductId,
                    x.ProductName,
                    x.Price,
                    x.Quantity))
                .ToList();
          
              var integrationEvent = new OrderCreatedEvent(
                notification.DomainEvent.OrderId, notification.DomainEvent.CustomerId,
                items
            );
          await  integrationEventBus.PublishAsync(integrationEvent);

        }
    }
