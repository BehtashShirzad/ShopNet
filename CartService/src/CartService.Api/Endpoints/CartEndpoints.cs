 
using CartService.Application.Commands;
using CartService.Application.Query;
using Mapster;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using ShopNet.Authorization;

namespace CartService.Api
{
    public static class CartEndpoints 
    {
       public static IEndpointRouteBuilder MapCartEndpoints(this IEndpointRouteBuilder endpoint)
        {
            var map = endpoint.MapGroup("cart");
            map.AddEndpointFilter<CartFailureFilter>();
            map.MapPost("/items", AddCart).RequireAuthorization(CartPermissions.Write);
            map.MapGet("/{cartId}",GetCartItems).RequireAuthorization(CartPermissions.Read);
            map.MapPut("/items/{cartId}",AddProductToCart).RequireAuthorization(CartPermissions.Write);
            map.MapPost("/checkout/{cartId}",Checkout)
                .RequireAuthorization(CartPermissions.Checkout);
            return map;
        }

        private static async Task<IResult> AddProductToCart([FromBody]ProductViewModelInput dto,
        [FromServices]ISender sender,[FromServices]IHttpContextAccessor contextAccessor,[FromRoute] Guid cartId)
        {
           var mapped = dto.Adapt<AddProductToCartCommand>();
            mapped.UserId = contextAccessor.GetUserId();
            mapped.CartId = cartId;
            mapped.ProductDto = dto;
            await sender.Send(mapped);
            return TypedResults.Ok();
        }

        private static async Task<IResult> GetCartItems([FromServices]ISender sender,[FromRoute]Guid cartId,
        [FromServices]IHttpContextAccessor httpContextAccessor)
        {
              var cart = await sender.Send(
        new UserCartQuery(cartId,httpContextAccessor.GetUserId())
                    );

    return Results.Ok(cart);
        }

        private static async Task<IResult> AddCart([FromBody]AddCartCommand dto,
        [FromServices]ISender sender,IHttpContextAccessor contextAccessor)
        {
            var mapped = dto.Adapt<AddCartCommand>();
            mapped.UserId = contextAccessor.GetUserId();
            var cartId = await sender.Send(mapped);
            return TypedResults.Ok(cartId);
             
        }

        private static async Task<IResult> Checkout([FromRoute]Guid CartId,[FromServices]IHttpContextAccessor contextAccessor,[FromServices]ISender sender, CancellationToken cancellationToken)
        {
            
            var userId = contextAccessor.GetUserId();
            var command   =  new CheckoutCartCommand(CartId,userId);
            var result =await sender.Send(command, cancellationToken);
            return TypedResults.Ok(result);

        }
    }
}
