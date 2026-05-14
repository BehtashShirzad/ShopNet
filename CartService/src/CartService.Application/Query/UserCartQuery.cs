using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Application.Abstractions.Contracts;
using CartService.Application.Commands;
using CartService.Domain;

namespace CartService.Application.Query
{
    public record UserCartQuery(Guid CartId,Guid UserId):IQuery<CartDto>;
    public class UserCartQueryHandler(IRepository repository) : IQueryHandler<UserCartQuery, CartDto>
    {
        public async Task<CartDto> Handle(UserCartQuery request, CancellationToken cancellationToken)
        {
            var cart =   await repository.GetCart(request.CartId);
            if(cart == null)
            {
             throw new Exception("Cart not found");
            }

            if(cart.CustomerId!= request.UserId)
                throw new Exception("Cart not found");
            var prodocts = cart
                            .Items
                            .Select(i => new ProductViewModelOutput(i.ProductId,i.Quantity,i.Price,i.ProductName))
                            .ToList();
            return new CartDto(prodocts, cart.TotalPrice);
        }
    }

}