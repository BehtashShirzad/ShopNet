using MediatR;
using Microsoft.AspNetCore.Mvc;
using OrderService.Application.Query.GetOrderById;
using ShopNet.Authorization;

namespace OrderService.Api;

public static class OrderEndpoints
{
    
    public static IEndpointRouteBuilder MapEndpoint(this IEndpointRouteBuilder builder)
    {
        var gp = builder.MapGroup("orders");
        gp.MapGet("/{orderId:guid}", GetOrderById)
            .RequireAuthorization(OrderPermissions.ReadOwn);
        return gp;
    }

    private static async Task<IResult> GetOrderById([FromRoute]Guid orderId,[FromServices]ISender sender,[FromServices]IHttpContextAccessor httpContextAccessor)
    {
        var userId = httpContextAccessor.GetUserId();
        var result = await sender.Send(new GetOrderByIdQuery(orderId,userId));
        return TypedResults.Ok(result);
    }
}
