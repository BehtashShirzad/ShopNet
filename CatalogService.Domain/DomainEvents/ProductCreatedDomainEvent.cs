using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SharedKernel.Domain;

namespace CatalogService.Domain.DomainEvents
{
    public record ProductCreatedDomainEvent : IDomainEvent<Guid>
    {
        public ProductCreatedDomainEvent(Guid id)
        {
            OccurredOn = DateTime.UtcNow;
            Id=id;
            
        }
        public Guid Id {get;set;}

        public DateTime OccurredOn{get;init;}
    }
}