 
using CartService.Application.Commands;
using CartService.Application.Query;
using Mapster;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace CartService.Api
{
    public static class CartEndpoints 
    {
       public static IEndpointRouteBuilder MapCartEndpoints(this IEndpointRouteBuilder endpoint)
        {
            var map = endpoint.MapGroup("cart");
            map.MapPost("/items", AddCart);
            map.MapGet("",GetCartItems);
            map.MapPut("/items",AddProductToCart);
            return map;
        }

        private static async Task<IResult> AddProductToCart([FromBody]ProductDto dto,[FromServices]ISender sender,[FromServices]IHttpContextAccessor contextAccessor)
        {
           var mapped = dto.Adapt<AddProductToCartCommand>();
            mapped.UserId = contextAccessor.GetUserId();
            await sender.Send(mapped);
            return TypedResults.Ok();
        }

        private static async Task<IResult> GetCartItems([FromServices]ISender sender,
        [FromServices]IHttpContextAccessor httpContextAccessor)
        {
              var cart = await sender.Send(
        new UserCartQuery(httpContextAccessor.GetUserId())
                    );

    return Results.Ok(cart);
        }

        private static async Task<IResult> AddCart([FromBody]AddCartCommand dto,
        [FromServices]ISender sender,IHttpContextAccessor contextAccessor)
        {
            var mapped = dto.Adapt<AddCartCommand>();
            mapped.UserId = contextAccessor.GetUserId();
            await sender.Send(mapped);
            return TypedResults.Created();
             
        }
    }
}