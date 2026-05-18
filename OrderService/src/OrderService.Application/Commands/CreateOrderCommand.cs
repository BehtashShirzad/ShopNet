using System;
using System.Collections.Generic;
using Application.Abstractions;
using Application.Abstractions.Contracts;
using OrderService.Domain;
using OrderService.Domain.Aggregates;
using ShopNet.Contracts.SharedDtos;


namespace OrderService.Application.Commands
{
    public record CreateOrderCommand( Guid CartId,
    Guid CustomerId,
    List<ProductDto> Items,
    decimal TotalPrice):ICommand;
    public class CreateOrderCommandHandler : ICommandHandler<CreateOrderCommand>
    {
        readonly IOrderRepository _repository;
        public CreateOrderCommandHandler(IOrderRepository  repository)
        {
            _repository = repository;
        }
        public async Task Handle(CreateOrderCommand request, CancellationToken cancellationToken)
        {
            var existing = await _repository.GetByCartId(request.CartId); //TODO : No Need Remove 
            if (existing != null)
                return; 
            var order =   OrderAggregate.Create(request.CustomerId,request.CartId);
            foreach (var item in request.Items)
            {
                order.AddItem(item.ProductId, item.ProductName,item.Price, item.Quantity);
            }
             
            
                await   _repository.AddAsync(order);
        
        }
    }
}