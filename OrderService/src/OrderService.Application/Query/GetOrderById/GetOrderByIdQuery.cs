using Application.Abstractions.Contracts;
using OrderService.Domain;
using OrderService.Domain.Enums;
using ShopNet.Contracts.SharedDtos;

namespace OrderService.Application.Query.GetOrderById;

public record GetOrderByIdQueryResponse(Guid OrderId,List<ProductDto> ProductDto,OrderStatus OrderStatus)
{
    public Guid? InventoryReservationRequestId { get; init; }
    public DateTimeOffset? InventoryReservationExpiresAtUtc { get; init; }
    public long InventoryReservationVersion { get; init; }
    public OrderInventoryStatus? InventoryStatus { get; init; }
    public string? InventoryFailureReason { get; init; }
}
public record GetOrderByIdQuery(Guid OrderId,Guid UserId):IQuery<GetOrderByIdQueryResponse>;
public class GetOrderByIdQueryHandler(IOrderRepository orderRepository):IQueryHandler<GetOrderByIdQuery,GetOrderByIdQueryResponse>
{
    public async Task<GetOrderByIdQueryResponse> Handle(GetOrderByIdQuery request, CancellationToken cancellationToken)
    {
        var order = await orderRepository.GetAsync(
            x => x.Id == request.OrderId && x.CustomerId == request.UserId);

        if (order is null)
            throw new InvalidOperationException("Order not found");

        return new GetOrderByIdQueryResponse(
            order.Id,
            order.Items.Select(item => new ProductDto(
                item.ProductId,
                item.ProductName,
                item.Price,
                item.Quantity)).ToList(),
            order.Status)
        {
            InventoryReservationRequestId = order.InventoryReservationRequestId,
            InventoryReservationExpiresAtUtc = order.InventoryReservationExpiresAtUtc,
            InventoryReservationVersion = order.InventoryReservationVersion,
            InventoryStatus = order.InventoryStatus,
            InventoryFailureReason = order.InventoryFailureReason
        };

    }
}
