using Application.Abstractions.Contracts;
using MassTransit.Initializers;
using OrderService.Domain;
using OrderService.Domain.Enums;
using ShopNet.Contracts.SharedDtos;

namespace OrderService.Application.Query.GetOrderById;

public record GetOrderByIdQueryResponse(Guid OrderId,List<ProductDto> ProductDto,OrderStatus OrderStatus);
public record GetOrderByIdQuery(Guid OrderId,Guid UserId):IQuery<GetOrderByIdQueryResponse>;
public class GetOrderByIdQueryHandler(IOrderRepository orderRepository):IQueryHandler<GetOrderByIdQuery,GetOrderByIdQueryResponse>
{
    public Task<GetOrderByIdQueryResponse> Handle(GetOrderByIdQuery request, CancellationToken cancellationToken)
    {

        return orderRepository.GetAsync(_ => _.Id == request.OrderId && _.CustomerId == request.UserId)
            .Select(_=>new GetOrderByIdQueryResponse(_.Id,
                _.Items.Select(z=>
                    new ProductDto(z.ProductId,
                        z.ProductName,
                        z.Price,
                        z.Quantity)).ToList()
                ,_.Status));

    }
}