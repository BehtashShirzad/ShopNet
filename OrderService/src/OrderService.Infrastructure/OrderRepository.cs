using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Application.Abstractions.Contracts;
using Microsoft.EntityFrameworkCore;
using OrderService.Domain;
using OrderService.Domain.Aggregates;

namespace OrderService.Infrastructure
{
    public class OrderRepository(IApplicationDbContext writeDbContext): IOrderRepository
    {
        readonly IApplicationDbContext _context= writeDbContext;
        public async Task AddAsync(OrderAggregate order)
        {
            await _context.Set<OrderAggregate>().AddAsync(order);
            
        }

        public async Task<OrderAggregate?> GetByCartId(Guid cartId)
        {
             return await _context.Set<OrderAggregate>().Where(o => o.CartId == cartId).FirstOrDefaultAsync();
        }

        public Task<OrderAggregate?> GetAsync(Expression<Func<OrderAggregate, bool>> predicate)
        {
            return   _context.Set<OrderAggregate>().AsNoTracking().Where(predicate).FirstOrDefaultAsync();
        }

        public async Task<OrderAggregate?> GetByIdAsync(Guid id)
        {
              return await _context.Set<OrderAggregate>().Where(o => o.Id == id).FirstOrDefaultAsync();
        }
    }
}