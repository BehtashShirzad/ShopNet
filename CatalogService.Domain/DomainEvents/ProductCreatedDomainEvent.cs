using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Domain.Abstractions;

namespace CatalogService.Domain.DomainEvents
{
    public record ProductCreatedDomainEvent : IDomainEvent 
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