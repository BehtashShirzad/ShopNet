using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CatalogService.Application.Features.Product.Commands.UpdateProduct;
using CatalogService.Application.Features.Product.CreateProduct;
using CatalogService.Application.Features.Product.Queries.GetProducts;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace CatalogService.Api.Routes
{
    public static class ProductEndpoint
    {

        public static IEndpointRouteBuilder MapProductEndpoints(this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("products");

            group.MapPost("", CreateProduct)
            .Produces<CreateProductCommandResponse>()
            .ProducesProblem(400);

            group.MapGet("", GetProducts)
                .Produces<CreateProductCommandResponse>()
                .ProducesProblem(400);

            group.MapPut("", UpdateProduct);



            return app;
        }

        private static async Task<IResult> UpdateProduct([FromServices] IMediator mediator, [FromBody] UpdateProductCommand updateProductCommand)
        {
            var result = await mediator.Send(updateProductCommand);
            if (result)
                return TypedResults.Ok();
            else
                return TypedResults.BadRequest();
        }

        private static async Task<IResult> GetProducts([FromServices] IMediator mediator)
        {

            var product = await mediator.Send(new GetProductsQuery());
            return TypedResults.Ok(product);
        }

        private static async Task<IResult> CreateProduct([FromBody] CreateProductCommand request, [FromServices] IMediator mediator)
        {
            var product = await mediator.Send(request);
            return TypedResults.Ok(product);
        }


    }
}