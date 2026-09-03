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

  
    public record AddCartCommand(List<ProductViewModelInput> Products) : ICommand<Guid>
    {
        public Guid UserId{get;set;}

    }

    public class AddCartCommandHandler(IRepository repository,ICatalogService catalogService) : 
    ICommandHandler<AddCartCommand,Guid>
    {
        public async Task<Guid> Handle(AddCartCommand request, CancellationToken cancellationToken)
        {
          var cart = CartAggregate.Create(request.UserId);
          if (request.Products is null || request.Products.Count > 100 || request.Products.Any(x => x is null || x.Quantity <= 0))
              throw new ArgumentException("Provide at most 100 valid product lines.");
          foreach(var item in request.Products)
            {
                var product = await catalogService.GetProduct(item.ProductId, cancellationToken);
                if(product is null)
                 throw new Exception("Product Not Found");

                cart.AddItem(product.Id, product.Name, product.Price, item.Quantity);
            }

            await repository.StoreCart(cart);

            return cart.Id;
        
        }
    }
}
