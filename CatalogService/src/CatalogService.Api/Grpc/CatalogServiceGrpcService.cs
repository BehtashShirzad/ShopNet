using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CatalogService.API.Grpc.Protos;
using CatalogService.Application.Features.Product.Queries.GetProduct;
 
using Grpc.Core;
using MassTransit.Mediator;
using MediatR;

namespace CatalogService.Api.Grpc
{
    public class CatalogServiceGrpcService(ISender sender):CatalogProtoService.CatalogProtoServiceBase
    {

        public override async Task<ProductResponse> GetProduct(GetProductRequest request, ServerCallContext context)
        {
         
         var result =  await  sender.Send(new GetProductQuery(Guid.Parse(request.ProductId)));
            return new ProductResponse
            {
                Id = result?.Id.ToString(),
                Name = result?.Name,
                Price = Convert.ToDouble(result?.Price),
                Stock = result?.Stock??0
            };
        }
        
    }
}