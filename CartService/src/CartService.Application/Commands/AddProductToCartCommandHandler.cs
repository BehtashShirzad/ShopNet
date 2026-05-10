using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Application.Abstractions;
using Application.Abstractions.Contracts;
using CartService.Domain.Aggregates;

namespace CartService.Application.Commands
{
    public record AddProductToCartCommand(ProductDto ProductDto) : ICommand
    {
        
        public Guid UserId{get;set;}
    }
    public class AddProductToCartCommandHandler : ICommandHandler<AddProductToCartCommand>
    {
        public Task Handle(AddProductToCartCommand request, CancellationToken cancellationToken)
        {
            //fetch cart
              var cart = CartAggregate.Create(request.UserId);

            cart.AddItem(request.ProductDto.ProductId,request.ProductDto.ProductName,request.ProductDto.Price,request.ProductDto.Quantity);


            // Update Redis

            return Task.CompletedTask;

        }
    }

}