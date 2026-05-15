using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using OrderService.Domain;
using OrderService.Domain.Aggregates;

namespace OrderService.Infrastructure
{
    public class OrderRepository(WriteDbContext writeDbContext): IOrderRepository
    {
        readonly WriteDbContext _context= writeDbContext;
        public async Task AddAsync(OrderAggregate order)
        {
            await _context.Orders.AddAsync(order);
            
        }

        public async Task<OrderAggregate?> GetByCartId(Guid cartId)
        {
             return await _context.Orders.Where(o => o.CartId == cartId).FirstOrDefaultAsync();
        }

        public async Task<OrderAggregate?> GetByIdAsync(Guid id)
        {
              return await _context.Orders.Where(o => o.Id == id).FirstOrDefaultAsync();
        }
    }
}