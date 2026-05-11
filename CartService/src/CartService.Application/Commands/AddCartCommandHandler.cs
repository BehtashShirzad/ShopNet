using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Application.Abstractions;
using Application.Abstractions.Contracts;
using CartService.Domain;
using CartService.Domain.Aggregates;

namespace CartService.Application.Commands
{ 

  
    public record AddCartCommand(List<ProductDto> Products) : ICommand<Guid>
    {
        public Guid UserId{get;set;}

    }

    public class AddCartCommandHandler(IRepository repository) : 
    ICommandHandler<AddCartCommand,Guid>
    {
        public async Task<Guid> Handle(AddCartCommand request, CancellationToken cancellationToken)
        {
          var cart = CartAggregate.Create(request.UserId);
          foreach(var item in request.Products)
            {
                // Better To Get Products From CatalogService
                cart.AddItem(item.ProductId,item.ProductName,item.Price,item.Quantity);
            }

            await repository.StoreCart(cart);

            return cart.Id;
        
        }
    }
}