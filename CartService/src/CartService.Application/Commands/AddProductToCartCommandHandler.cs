using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Application.Abstractions;
using Application.Abstractions.Contracts;
using CartService.Domain;
using CartService.Domain.Aggregates;

namespace CartService.Application.Commands
{
    public record AddProductToCartCommand : ICommand
    {
        [JsonIgnore]
        public Guid UserId{get;set;}
        [JsonIgnore]
        public Guid CartId{get;set;}
        public ProductViewModelInput ProductDto{get;set;}=null!;
    }
    public class AddProductToCartCommandHandler(IRepository repository,ICatalogService catalogService) : ICommandHandler<AddProductToCartCommand>
    {
        public async Task Handle(AddProductToCartCommand request, CancellationToken cancellationToken)
        {
           
              
              var cart = await repository.GetCart(request.CartId);
        if(cart is null)
        throw new Exception("Cart not found");
          if(cart.CustomerId!= request.UserId)
                throw new Exception("Cart not found");
         var product = catalogService.GetProduct(request.ProductDto.ProductId);
                if(product is null)
                 throw new Exception("Product Not Found");
            cart.AddItem(request.ProductDto.ProductId,
            request.ProductDto.ProductName,
            request.ProductDto.Price,
            request.ProductDto.Quantity);


           await repository. StoreCart(cart);

     

        }
    }

}