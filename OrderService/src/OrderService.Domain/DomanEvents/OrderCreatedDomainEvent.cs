using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Domain.Abstractions;

namespace OrderService.Domain.DomanEvents
{
    public class OrderCreatedDomainEvent : IDomainEvent
    {
        public OrderCreatedDomainEvent(Guid orderId)
        {
            Id=IdGenerator.New();
            OrderId =orderId;
            OccurredOn = DateTime.UtcNow;
        }
        
        public Guid Id{get; private set;} 
        public Guid OrderId { get; private set; }

        public DateTime OccurredOn {get;private set;}
    }
}