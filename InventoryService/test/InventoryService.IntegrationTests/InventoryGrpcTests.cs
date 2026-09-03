using Grpc.Core;
using InventoryService.Grpc.V1;
using InventoryService.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace InventoryService.IntegrationTests;

[Collection("Inventory containers")]
public sealed class InventoryGrpcTests(InventoryContainers containers)
{
    [Fact]
    public async Task BatchAvailability_UsesRealGrpcAndDoesNotReserve()
    {
        await using var host = await InventoryTestHost.Create(containers);
        var product = await host.Seed();
        var missing = Guid.NewGuid();
        await host.Run(x => x.ReserveAsync(host.Request(new ShopNet.Contracts.Inventory.V1.InventoryLine(product, 3))));
        await host.Start();
        using var channel = host.Channel();
        var client = new InventoryAvailabilityService.InventoryAvailabilityServiceClient(channel);
        var request = new GetAvailabilityRequest();
        request.ProductIds.Add([product.ToString(), missing.ToString(), product.ToString()]);
        var response = await client.GetAvailabilityAsync(request);
        Assert.Equal(2, response.Items.Count);
        var available = Assert.Single(response.Items, x => x.ProductId == product.ToString());
        Assert.True(available.Exists);
        Assert.True(available.IsActive);
        Assert.Equal(7, available.AvailableQuantity);
        var unknown = Assert.Single(response.Items, x => x.ProductId == missing.ToString());
        Assert.False(unknown.Exists);
        Assert.Equal(0, unknown.AvailableQuantity);
        Assert.Equal(3, (await host.Item(product))!.ReservedQuantity);
        Assert.Equal(1, await host.Pending());
    }

    [Theory]
    [InlineData("empty")]
    [InlineData("malformed")]
    [InlineData("zero")]
    [InlineData("tooMany")]
    public async Task InvalidRequest_ReturnsInvalidArgument(string kind)
    {
        await using var host = await InventoryTestHost.Create(containers);
        await host.Start();
        using var channel = host.Channel();
        var client = new InventoryAvailabilityService.InventoryAvailabilityServiceClient(channel);
        var request = new GetAvailabilityRequest();
        if (kind == "malformed") request.ProductIds.Add("not-a-guid");
        if (kind == "zero") request.ProductIds.Add(Guid.Empty.ToString());
        if (kind == "tooMany") request.ProductIds.Add(Enumerable.Range(0, 101).Select(_ => Guid.NewGuid().ToString()));
        var exception = await Assert.ThrowsAsync<RpcException>(async () => await client.GetAvailabilityAsync(request));
        Assert.Equal(StatusCode.InvalidArgument, exception.StatusCode);
    }

    [Fact]
    public async Task InactiveProduct_ReportsNoSellableStock()
    {
        await using var host = await InventoryTestHost.Create(containers);
        var product = await host.Seed();
        await using (var scope = host.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
            (await db.InventoryItems.SingleAsync(x => x.ProductId == product)).Deactivate();
            await db.SaveChangesAsync();
        }
        await host.Start();
        using var channel = host.Channel();
        var client = new InventoryAvailabilityService.InventoryAvailabilityServiceClient(channel);
        var request = new GetAvailabilityRequest();
        request.ProductIds.Add(product.ToString());
        var item = Assert.Single((await client.GetAvailabilityAsync(request)).Items);
        Assert.True(item.Exists);
        Assert.False(item.IsActive);
        Assert.Equal(0, item.AvailableQuantity);
    }
}
