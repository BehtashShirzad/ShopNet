using Domain.Abstractions;

namespace CatalogService.Domain.DomainEvents;

public sealed record ProductCreatedDomainEvent : IDomainEvent
{
    public ProductCreatedDomainEvent(Guid productId)
    {
        if (productId == Guid.Empty)
            throw new ArgumentException("ProductId cannot be empty.", nameof(productId));

        ProductId = productId;
    }

    public Guid Id { get; init; } = IdGenerator.New();
    public Guid ProductId { get; }
    public DateTime OccurredOn { get; init; } = DateTime.UtcNow;
}
