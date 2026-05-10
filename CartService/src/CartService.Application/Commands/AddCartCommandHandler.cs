using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Application.Abstractions;
using Application.Abstractions.Contracts;
using CartService.Domain.Aggregates;

namespace CartService.Application.Commands
{ 

  
    public record AddCartCommand(List<ProductDto> Products) : ICommand
    {
        public Guid UserId{get;set;}

    }

    public class AddCartCommandHandler : ICommandHandler<AddCartCommand>
    {
        public Task Handle(AddCartCommand request, CancellationToken cancellationToken)
        {
          var cart = CartAggregate.Create(request.UserId);
          foreach(var item in request.Products)
            {
                // Better To Get Products From CatalogService
                cart.AddItem(item.ProductId,item.ProductName,item.Price,item.Quantity);
            }

            // Insert To Redis(Store)

            return Task.CompletedTask;
        
        }
    }
}