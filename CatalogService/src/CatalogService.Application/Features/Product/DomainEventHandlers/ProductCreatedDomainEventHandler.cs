using Application.Abstractions;
using Application.Abstractions.Contracts;
using CatalogService.Domain.DomainEvents;
using MediatR;
using ProductCreatedV1 = ShopNet.Contracts.IntegrationEvents.Catalog.V1.ProductCreated;

namespace CatalogService.Application.Features.Product.DomainEventHandlers;

public sealed class ProductCreatedDomainEventHandler(IIntegrationEventBus eventBus)
    : INotificationHandler<DomainEventNotification<ProductCreatedDomainEvent>>
{
    public Task Handle(
        DomainEventNotification<ProductCreatedDomainEvent> notification,
        CancellationToken cancellationToken)
    {
        var domainEvent = notification.DomainEvent;
        var message = new ProductCreatedV1(domainEvent.ProductId)
        {
            EventId = domainEvent.Id,
            OccurredOnUtc = new DateTimeOffset(domainEvent.OccurredOn)
        };

        return eventBus.PublishAsync(message, cancellationToken);
    }
}
