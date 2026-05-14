using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Domain.Abstractions;
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

        private OrderAggregate()
        {
            
        }
        
       

        public static OrderAggregate Create(Guid customerId)
        {
            
            return new OrderAggregate()
            {
             Id=IdGenerator.New(),
             Status = OrderStatus.Created,  
             CustomerId = customerId
            };
        }
        public void AddItem(Guid productId, string name, decimal price, int quantity)
        {
            _items.Add(new OrderItem(productId, name, price, quantity));
        }
    }
}