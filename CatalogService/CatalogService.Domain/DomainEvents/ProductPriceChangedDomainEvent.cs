using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Domain.Abstractions;

namespace CatalogService.Domain.DomainEvents
{
    public class ProductPriceChangedDomainEvent:IDomainEvent
    {
         public ProductPriceChangedDomainEvent(Guid id)
        {
            OccurredOn = DateTime.UtcNow;
            Id=id;
            
        }
        public Guid Id {get;set;}

        public DateTime OccurredOn{get;init;}
    }
}