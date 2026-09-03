using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Domain.Abstractions;

namespace CatalogService.Domain.DomainEvents
{
    public class ProductUpdatedDomainEvent : IDomainEvent
    {
        public ProductUpdatedDomainEvent(Guid id)
        
        {
        Id=id;
        OccurredOn = DateTime.UtcNow;    
        }
        public Guid Id {get;init;}

        public DateTime OccurredOn {get;init;}
    }
}
