using InventoryService.Application;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using ShopNet.Contracts.Inventory.V1;
using ProductCreated = ShopNet.Contracts.IntegrationEvents.Catalog.V1.ProductCreated;

namespace InventoryService.Infrastructure;

// Each delivery invokes an application operation in an independent scope. This is intentional:
// the operation owns SQL + BusOutbox, without inheriting a ConsumeContext publish endpoint.
// Business request/receipt IDs provide permanent deduplication even with a different MessageId.
// Returning from Consume acknowledges only after that operation has committed successfully.
public sealed class InventoryCommandConsumer(IServiceScopeFactory scopes) :
    IConsumer<ReserveInventory>, IConsumer<CommitInventory>, IConsumer<ReleaseInventory>
{
    private async Task Run(Func<InventoryOperations, Task> action)
    {
        await using var scope = scopes.CreateAsyncScope();
        await action(scope.ServiceProvider.GetRequiredService<InventoryOperations>());
    }
    public Task Consume(ConsumeContext<ReserveInventory> context)
        => Run(x => x.ReserveAsync(context.Message, context.CancellationToken));
    public Task Consume(ConsumeContext<CommitInventory> context)
        => Run(x => x.CommitAsync(context.Message, context.CancellationToken));
    public Task Consume(ConsumeContext<ReleaseInventory> context)
        => Run(x => x.ReleaseAsync(context.Message, context.CancellationToken));
}

public sealed class ProductCreatedConsumer(IServiceScopeFactory scopes) : IConsumer<ProductCreated>
{
    public async Task Consume(ConsumeContext<ProductCreated> context)
    {
        await using var scope = scopes.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<InventoryOperations>()
            .RegisterProductAsync(context.Message.ProductId, context.CancellationToken);
    }
}

public sealed class ReceiveStockConsumer(IServiceScopeFactory scopes) : IConsumer<ReceiveInventoryStock>
{
    public async Task Consume(ConsumeContext<ReceiveInventoryStock> context)
    {
        await using var scope = scopes.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<InventoryOperations>()
            .ReceiveStockAsync(context.Message, context.CancellationToken);
    }
}
