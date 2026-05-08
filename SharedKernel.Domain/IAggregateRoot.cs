using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SharedKernel.Domain
{
    public interface IAggregateRoot<TID>
    {
           IReadOnlyCollection<IDomainEvent<TID>> DomainEvents { get; }
        void RaiseEvent(IDomainEvent<TID> domainEvent);
        void ClearEvents();
    }
}