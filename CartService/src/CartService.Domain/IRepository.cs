using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CartService.Domain.Aggregates;

namespace CartService.Domain
{
    public interface IRepository
    {
        public Task StoreCart(CartAggregate cart);
        public Task<CartAggregate?> GetCart(Guid cartId);
    }
}