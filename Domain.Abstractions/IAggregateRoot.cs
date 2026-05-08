using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Domain.Abstractions
{
    public interface IAggregateRoot
    {
        void ClearEvents();
        IReadOnlyCollection<IDomainEvent> DomainEvents { get; }
        void RaiseEvent(IDomainEvent domainEvent);
    }
   
}