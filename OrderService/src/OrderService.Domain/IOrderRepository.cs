using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OrderService.Domain
{
    public interface IOrderRepository
    {
        public Task AddAsync(Aggregates.OrderAggregate order);
         public Task<Aggregates.OrderAggregate?> GetByIdAsync(Guid id);
         public Task<Aggregates.OrderAggregate?> GetByCartId(Guid cartId);
         
    }
}