using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CatalogService.Application.Features.Category.Commands;
using CatalogService.Application.Features.Category.Commands.CreateCategory;
using CatalogService.Application.Features.Category.Commands.UpdateCategory;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace CatalogService.Api.Endpoints
{
    public static class CategoryEndpoints
    {
        public static IEndpointRouteBuilder MapCategoryEndpoints(this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("Category");
            group.MapPost("", CraeteCategory);
              group.MapPut("", UpdateCategory);

            return group;
        }

        private static async Task<IResult> UpdateCategory(  [FromBody]UpdateCategoryCommand request,  [FromServices] IMediator mediator )
        {
            var result =await mediator.Send(request);
            if(result)
            return TypedResults.Ok();
            else
            return TypedResults.BadRequest();
        }

        private static async Task<IResult> CraeteCategory([FromBody] CreateCategoryCommand command, [FromServices] IMediator mediator)
        {
            var category =await mediator.Send(command);
          return  TypedResults.Ok(category);
        }
    }
}