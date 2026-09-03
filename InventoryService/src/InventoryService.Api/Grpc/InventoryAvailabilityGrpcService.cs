using Grpc.Core;
using InventoryService.Application;
using InventoryService.Grpc.V1;

namespace InventoryService.Api.Grpc;

public sealed class InventoryAvailabilityGrpcService(IInventoryStore store)
    : InventoryAvailabilityService.InventoryAvailabilityServiceBase
{
    public override async Task<GetAvailabilityResponse> GetAvailability(
        GetAvailabilityRequest request, ServerCallContext context)
    {
        if (request.ProductIds.Count is 0 or > InventoryOperations.MaxBatchSize)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Provide 1-100 product IDs."));
        var ids = new List<Guid>();
        foreach (var value in request.ProductIds)
        {
            if (!Guid.TryParse(value, out var id) || id == Guid.Empty)
                throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid product ID."));
            ids.Add(id);
        }
        var result = await store.GetAvailabilityAsync(ids.Distinct().ToArray(), context.CancellationToken);
        var response = new GetAvailabilityResponse();
        response.Items.AddRange(result.Select(x => new ProductAvailability
        {
            ProductId = x.ProductId.ToString(), Exists = x.Exists,
            IsActive = x.IsActive, AvailableQuantity = x.AvailableQuantity
        }));
        return response;
    }
}
