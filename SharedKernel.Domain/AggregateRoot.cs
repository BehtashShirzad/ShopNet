 

namespace SharedKernel.Domain
{
    public class AggregateRoot<TID>:Entity<TID>, IAggregateRoot<TID>  where TID : IEquatable<TID>
    {
        private readonly List<IDomainEvent<TID>> _domainEvents = new();

        public IReadOnlyCollection<IDomainEvent<TID>> DomainEvents => _domainEvents.AsReadOnly();

        protected  AggregateRoot() { }

        protected AggregateRoot(TID id)
            : base(id) { }

        public void RaiseEvent(IDomainEvent<TID> domainEvent) => _domainEvents.Add(domainEvent);

        public void ClearEvents() => _domainEvents.Clear();
    }
}