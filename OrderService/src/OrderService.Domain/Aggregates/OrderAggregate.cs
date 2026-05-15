using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Ardalis.GuardClauses;
using Domain.Abstractions;
using OrderService.Domain.DomanEvents;
using OrderService.Domain.Enums;
using OrderService.Domain.ValueObjects;

namespace OrderService.Domain.Aggregates
{
    public class OrderAggregate:AggregateRoot<Guid>
    {
         public Guid CustomerId{get;init;}
        private readonly List<OrderItem> _items = new();
        public IReadOnlyCollection<OrderItem> Items => _items;
        public decimal TotalPrice => _items.Sum(x=>x.Price * x.Quantity);
        public OrderStatus Status{get; private set;}
        public Guid CartId { get; private set; }
        private OrderAggregate()
        {
            
        }
        
       

        public static OrderAggregate Create(Guid customerId, Guid cartId)
        {
            
            var order =  new OrderAggregate()
            {
             Id=IdGenerator.New(),
             Status = OrderStatus.Pending,  
             CustomerId = customerId,
             CartId = cartId
            };
            order.RaiseEvent(new OrderCreatedDomainEvent(order.Id));
            return order;
        }

       public void AddItem(Guid productId, string name, decimal price, int quantity)
        {
            if (Status != OrderStatus.Pending)
                throw new InvalidOperationException("Cannot modify order");

            if (quantity <= 0)
                
                Guard.Against.NullOrOutOfRange(quantity,nameof(quantity), 1, int.MaxValue, "Quantity must be greater than zero");
              

            if (price <= 0)
               Guard.Against.NullOrOutOfRange(price,nameof(price), 0.01m, decimal.MaxValue, "Price must be greater than zero");

            _items.Add(new OrderItem(productId, name, price, quantity));
        }

    }
}