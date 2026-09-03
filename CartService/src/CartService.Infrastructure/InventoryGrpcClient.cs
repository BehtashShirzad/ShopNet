using CartService.Application.Checkout;
using Grpc.Core;
using InventoryService.Grpc.V1;

namespace CartService.Infrastructure;

public sealed class InventoryGrpcClient(InventoryAvailabilityService.InventoryAvailabilityServiceClient client,
    GrpcCallOptions options) : IInventoryAvailabilityClient
{
    public async Task<IReadOnlyDictionary<Guid, InventoryAvailability>> GetAvailabilityAsync(Guid[] productIds, CancellationToken ct)
    {
        if (productIds.Length is 0 or > 100 || productIds.Any(x => x == Guid.Empty))
            throw new ArgumentException("Provide 1-100 valid product IDs.");
        var ids = productIds.Distinct().ToArray();
        var request = new GetAvailabilityRequest();
        request.ProductIds.AddRange(ids.Select(x => x.ToString()));
        var response = await client.GetAvailabilityAsync(request, deadline: DateTime.UtcNow.Add(options.Timeout), cancellationToken: ct);
        var result = new Dictionary<Guid, InventoryAvailability>();
        foreach (var item in response.Items)
        {
            if (!Guid.TryParse(item.ProductId, out var id) || !ids.Contains(id) || item.AvailableQuantity < 0 ||
                (!item.Exists && (item.IsActive || item.AvailableQuantity != 0)) ||
                !result.TryAdd(id, new(id, item.Exists, item.IsActive, item.AvailableQuantity)))
                throw new RpcException(new Status(StatusCode.DataLoss, "Invalid Inventory response."));
        }
        if (result.Count != ids.Length)
            throw new RpcException(new Status(StatusCode.DataLoss, "Incomplete Inventory response."));
        return result;
    }
}
