using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CartService.Application;
using CartService.Application.Commands;
using CartService.Application.Query;
using CatalogService.API.Grpc.Protos;

namespace CartService.Infrastructure
{
   public class CatalogGrpcClient : ICatalogService
{
    private readonly CatalogProtoService.CatalogProtoServiceClient _client;

    public CatalogGrpcClient(
        CatalogProtoService.CatalogProtoServiceClient client)
    {
        _client = client;
    }
    
    public async Task<GetProductDto?> GetProduct(Guid productId)
    {
        var response = await _client.GetProductAsync(
            new GetProductRequest
            {
                ProductId = productId.ToString()
            });

        return new GetProductDto(
            Guid.Parse(response.Id),
            response.Name,
            (decimal)response.Price,
            response.Stock);
    }
}

    
    
}