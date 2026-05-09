using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Domain.Abstractions;
namespace CartService.Domain.DomainEvents
{
    public class CartCreatedDomainEvent:IDomainEvent
    {
       
        
        public CartCreatedDomainEvent(Guid cartId)
        {
            Id = cartId;
            OccurredOn = DateTime.UtcNow;
        }
         public Guid Id{get;init;}
         public    DateTime OccurredOn {get;init;} 

        
    }
}