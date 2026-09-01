 

namespace Domain.Abstractions
{
 

public abstract class AggregateRoot : Entity, IAggregateRoot
{
    private readonly List<IDomainEvent> _domainEvents = new();

    public IReadOnlyCollection<IDomainEvent> DomainEvents 
        => _domainEvents.AsReadOnly();

    public void RaiseEvent(IDomainEvent domainEvent)
        => _domainEvents.Add(domainEvent);

    public void ClearEvents()
        => _domainEvents.Clear();
}



public abstract class AggregateRoot<TID> 
    : Entity<TID>, IAggregateRoot
    where TID : IEquatable<TID>
{
    private readonly List<IDomainEvent> _domainEvents = new();

    public IReadOnlyCollection<IDomainEvent> DomainEvents 
        => _domainEvents.AsReadOnly();

    protected AggregateRoot() { }

    protected AggregateRoot(TID id) : base(id) { }

    public void RaiseEvent(IDomainEvent domainEvent)
        => _domainEvents.Add(domainEvent);

    public void ClearEvents()
        => _domainEvents.Clear();
 
    }
}