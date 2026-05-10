using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Application.Abstractions.Contracts;
using CartService.Application.Commands;

namespace CartService.Application.Query
{
    public record UserCartQuery(Guid UserId):IQuery<CartDto>;
    public class UserCartQueryHandler : IQueryHandler<UserCartQuery, CartDto>
    {
        public async Task<CartDto> Handle(UserCartQuery request, CancellationToken cancellationToken)
        {
            return  new CartDto(new List<ProductDto>(){new ProductDto(Guid.Empty,2,1000,"test")},22222);
        }
    }

}