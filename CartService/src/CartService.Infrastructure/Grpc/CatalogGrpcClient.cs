using CartService.Application;
using CartService.Application.Query;
using CatalogService.API.Grpc.Protos;
using Grpc.Core;

namespace CartService.Infrastructure;

public sealed class CatalogGrpcClient(CatalogProtoService.CatalogProtoServiceClient client, GrpcCallOptions options) : ICatalogService
{
    public async Task<GetProductDto?> GetProduct(Guid productId, CancellationToken cancellationToken = default)
    {
        ProductResponse response;
        try
        {
            response = await client.GetProductAsync(new GetProductRequest { ProductId = productId.ToString() },
                deadline: DateTime.UtcNow.Add(options.Timeout), cancellationToken: cancellationToken);
        }
        catch (RpcException exception) when (exception.StatusCode == StatusCode.NotFound) { return null; }

        if (!Guid.TryParse(response.Id, out var id) || id != productId || id == Guid.Empty ||
            string.IsNullOrWhiteSpace(response.Name) || !double.IsFinite(response.Price) ||
            response.Price <= 0 || response.Price >= 10000000000000000d)
            throw new RpcException(new Status(StatusCode.DataLoss, "Invalid Catalog response."));
        var price = (decimal)response.Price;
        if (decimal.Round(price, 2) != price)
            throw new RpcException(new Status(StatusCode.DataLoss, "Catalog price must fit decimal(18,2)."));
        return new GetProductDto(id, response.Name, price);
    }
}
