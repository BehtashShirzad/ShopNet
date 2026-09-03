using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Domain.Abstractions;
using OrderService.Domain.ValueObjects;

namespace OrderService.Domain.DomainEvents
{
    public class OrderCreatedDomainEvent : IDomainEvent
    {
        public OrderCreatedDomainEvent(Guid orderId,List<OrderItem> orderItems,Guid customerId)
        {
            Id=IdGenerator.New();
            OrderId =orderId;
            OccurredOn = DateTime.UtcNow;
            OrderItems = orderItems;
            CustomerId = customerId;
        }
        
        public Guid Id{get; private set;} 
        public Guid OrderId { get; private set; }

        public DateTime OccurredOn {get;private set;}
        public List<OrderItem> OrderItems{get;private set;}
        public Guid CustomerId {get;private set;}
    }
}
