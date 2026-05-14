using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Application.Abstractions;
using Application.Abstractions.Contracts;
using CartService.Domain;
using ShopNet.Contracts.IntegrationEvents;
using ShopNet.Contracts.SharedDtos;

namespace CartService.Application.Commands
{
    public record CheckoutCartCommand(Guid CartId,Guid UserId) : ICommand<Guid>;
    public class CheckoutCartCommandHandler(IRepository repository,ICatalogService catalogService, IIntegrationEventBus integrationEventBus) : ICommandHandler<CheckoutCartCommand,Guid>
    {
        public async Task<Guid> Handle(CheckoutCartCommand request, CancellationToken cancellationToken)
        {
            var cart = await repository.GetCart(request.CartId);
                 if(cart is null)
            throw new Exception("Cart not found");
            if(cart.CustomerId!= request.UserId)
                throw new Exception("Cart not found");
            foreach(var item  in cart.Items)
            {
                var product =await catalogService.GetProduct(item.ProductId);
                if(product is null)
                 throw new Exception($"Product with id {item.ProductId} Not Found");
                 //Better to check stock in the catalog service and not here but for simplicity we will check it here
                if(product.Stock<item.Quantity)
                 throw new Exception($"Product with id {item.ProductId} is out of stock");
            }
       
         
            cart.Checkout();
            await repository.StoreCart(cart);
            //better to consider OutBox pattern for integration events but for simplicity we will publish the event directly
           var integrationEvent = new CartCheckedOutEvent(
                            cart.Id,
                            cart.CustomerId,
                            cart.Items.Select(item =>
                                new CartItemDto(
                                    item.ProductId,
                                    item.ProductName,
                                    item.Price,
                                    item.Quantity
                                )).ToList(),
                            cart.TotalPrice
                        );
            await integrationEventBus.PublishAsync(integrationEvent);
            return cart.Id;
        }
    }
     
}