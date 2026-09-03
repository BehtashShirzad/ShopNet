using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using OrderService.Domain.Aggregates;

namespace OrderService.Domain
{
    public interface IOrderRepository
    {
        public Task AddAsync(Aggregates.OrderAggregate order);
         public Task<Aggregates.OrderAggregate?> GetByIdAsync(Guid id);
         public Task<Aggregates.OrderAggregate?> GetByCartId(Guid cartId);
         public Task<OrderAggregate?> GetAsync(Expression<Func<OrderAggregate, bool>> predicate);

    }
}
