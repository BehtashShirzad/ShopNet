using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SharedKernel.Domain;

namespace CatalogService.Domain
{
    public class ProductUpdatedDomainEvent : IDomainEvent<Guid>
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