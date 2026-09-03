using CatalogService.API.Grpc.Protos;
using Google.Protobuf;
using Grpc.Core;
using InventoryService.Grpc.V1;

namespace CartService.IntegrationTests;

// Actual protobuf/HTTP2 calls, with controllable upstream behavior; no mock CallInvoker.
public sealed class CheckoutGrpcState
{
    public Guid ProductId { get; } = Guid.NewGuid();
    public double Price { get; set; } = 10;
    public string CatalogFailure { get; set; } = "";
    public string InventoryFailure { get; set; } = "";
    public int Available { get; set; } = 10;
    public bool Exists { get; set; } = true;
    public bool Active { get; set; } = true;
    public int CatalogCalls;
    public int InventoryCalls;
    public DateTime? InventoryDeadline;
    public TaskCompletionSource InventoryEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
}

[BindServiceMethod(typeof(CatalogGrpcStub), nameof(BindService))]
public class CatalogGrpcStub(CheckoutGrpcState state)
{
    public static void BindService(ServiceBinderBase binder, CatalogGrpcStub? service)
        => binder.AddMethod(new Method<GetProductRequest, ProductResponse>(MethodType.Unary,
            "catalog.CatalogProtoService", "GetProduct",
            Marshallers.Create(x => x.ToByteArray(), GetProductRequest.Parser.ParseFrom),
            Marshallers.Create(x => x.ToByteArray(), ProductResponse.Parser.ParseFrom)),
            service is null ? null : service.GetProduct);

    public virtual Task<ProductResponse> GetProduct(GetProductRequest request, ServerCallContext context)
    {
        Interlocked.Increment(ref state.CatalogCalls);
        if (state.CatalogFailure == "unavailable") throw new RpcException(new(StatusCode.Unavailable, "offline"));
        if (state.CatalogFailure == "missing") throw new RpcException(new(StatusCode.NotFound, "missing"));
        var response = new ProductResponse
        {
            Id = state.CatalogFailure == "identity" ? Guid.NewGuid().ToString() : request.ProductId,
            Name = "Product", Price = state.Price
        };
        // Legacy Catalog still sends field 5 (Stock). The Cart client must ignore even zero stock.
        var bytes = response.ToByteArray().Concat(new byte[] { 40, 0 }).ToArray();
        return Task.FromResult(ProductResponse.Parser.ParseFrom(bytes));
    }
}

[BindServiceMethod(typeof(InventoryGrpcStub), nameof(BindService))]
public class InventoryGrpcStub(CheckoutGrpcState state)
{
    public static void BindService(ServiceBinderBase binder, InventoryGrpcStub? service)
        => binder.AddMethod(new Method<GetAvailabilityRequest, GetAvailabilityResponse>(MethodType.Unary,
            "inventory.v1.InventoryAvailabilityService", "GetAvailability",
            Marshallers.Create(x => x.ToByteArray(), GetAvailabilityRequest.Parser.ParseFrom),
            Marshallers.Create(x => x.ToByteArray(), GetAvailabilityResponse.Parser.ParseFrom)),
            service is null ? null : service.GetAvailability);

    public virtual async Task<GetAvailabilityResponse> GetAvailability(GetAvailabilityRequest request, ServerCallContext context)
    {
        Interlocked.Increment(ref state.InventoryCalls);
        state.InventoryDeadline = context.Deadline;
        state.InventoryEntered.TrySetResult();
        if (state.InventoryFailure == "unavailable") throw new RpcException(new(StatusCode.Unavailable, "offline"));
        if (state.InventoryFailure == "delay")
        {
            try { await Task.Delay(TimeSpan.FromSeconds(30), context.CancellationToken); }
            catch (OperationCanceledException) { throw new RpcException(new(StatusCode.Cancelled, "request cancelled")); }
        }
        var response = new GetAvailabilityResponse();
        foreach (var id in request.ProductIds)
            response.Items.Add(new ProductAvailability
            {
                ProductId = state.InventoryFailure == "foreign" ? Guid.NewGuid().ToString() : id,
                Exists = state.Exists, IsActive = state.Active, AvailableQuantity = state.Available
            });
        if (state.InventoryFailure == "incomplete") response.Items.Clear();
        if (state.InventoryFailure == "duplicate") response.Items.Add(response.Items[0].Clone());
        return response;
    }
}
