using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Domain.Abstractions;
using MassTransit;
using MediatR;
using OrderService.Application.Commands;
using OrderService.Domain;
using ShopNet.Contracts.IntegrationEvents;

namespace OrderService.Application.IntegrationEventHandler
{
    public class CartCheckedOutEventHandler(ISender sender) : IConsumer<CartCheckedOutEvent>
    {
        public async Task Consume(ConsumeContext<CartCheckedOutEvent> context)
        {
            var createOrderCommand = new CreateOrderCommand(context.Message.CartId,
             context.Message.CustomerId,
              context.Message.Items, 
              context.Message.TotalPrice);
            await sender.Send(createOrderCommand);
            
        }
    }
}