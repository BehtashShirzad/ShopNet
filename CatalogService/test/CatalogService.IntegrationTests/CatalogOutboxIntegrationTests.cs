using System.Net.Http.Json;
using System.Text.Json;
using CatalogService.API.Grpc.Protos;
using CatalogService.Application.Features.Product.Commands.CreateProduct;
using CatalogService.Domain.Aggregates;
using CatalogService.Infrastructure;
using Google.Protobuf;
using Grpc.Core;
using Grpc.Net.Client;
using MassTransit.EntityFrameworkCoreIntegration;
using MediatR;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using ProductCreatedV1 = ShopNet.Contracts.IntegrationEvents.Catalog.V1.ProductCreated;

namespace CatalogService.IntegrationTests;

[Collection(CatalogContainersCollection.Name)]
public sealed class CatalogOutboxIntegrationTests(CatalogContainersFixture fixture)
{
    [Fact]
    public async Task HttpCreate_PersistsProductAndPublishesV1_WithoutBreakingLegacyGrpcStock()
    {
        await using var receiver = new ProductCreatedReceiver(fixture.RabbitMqConnectionString);
        await receiver.Start();
        await using var host = await CatalogOutboxTestHost.Create(fixture, deliveryEnabled: true);
        var categoryId = await host.SeedCategory();
        await host.Start();

        var response = await host.App.GetTestClient().PostAsJsonAsync("/products",
            new CreateProductCommand(categoryId, "Laptop", "Description", 1200m, 7));
        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<CreateProductCommandResponse>();
        Assert.NotNull(created);

        var delivered = await receiver.Receive(created.Id);
        Assert.Equal(created.Id, delivered.ProductId);
        Assert.NotEqual(Guid.Empty, delivered.EventId);
        Assert.NotEqual(created.Id, delivered.EventId);
        await host.WaitUntilOutboxDrained();

        await using var scope = host.App.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<WriteDbContext>();
        var stored = await context.Products.SingleAsync(x => x.Id == created.Id);
        Assert.Equal(7, stored.Stock);
        Assert.Equal(1200m, stored.Price);

        // Call the real HTTP/2 endpoint with the unchanged protobuf field numbers.
        using var channel = GrpcChannel.ForAddress("http://localhost",
            new GrpcChannelOptions { HttpHandler = host.App.GetTestServer().CreateHandler() });
        var method = new Method<GetProductRequest, ProductResponse>(
            MethodType.Unary, "catalog.CatalogProtoService", "GetProduct",
            Marshallers.Create(request => request.ToByteArray(), GetProductRequest.Parser.ParseFrom),
            Marshallers.Create(result => result.ToByteArray(), ProductResponse.Parser.ParseFrom));
        using var call = channel.CreateCallInvoker().AsyncUnaryCall(method, null,
            new CallOptions(deadline: DateTime.UtcNow.AddSeconds(15)),
            new GetProductRequest { ProductId = created.Id.ToString() });
        var grpcResult = await call.ResponseAsync;
        Assert.Equal(created.Id.ToString(), grpcResult.Id);
        Assert.Equal("Laptop", grpcResult.Name);
        Assert.Equal(7, grpcResult.Stock);
        Assert.Equal(1200d, grpcResult.Price);
    }

    [Fact]
    public async Task PausedDelivery_SurvivesHostRestart_AndPublishesOriginalEventWhenEnabled()
    {
        string connectionString;
        Guid productId;
        ProductCreatedV1 queuedMessage;
        await using (var firstHost = await CatalogOutboxTestHost.Create(fixture))
        {
            connectionString = firstHost.ConnectionString;
            var categoryId = await firstHost.SeedCategory();
            await firstHost.Start(); // Broker is running, but rollout gate disables delivery.
            await using var scope = firstHost.App.Services.CreateAsyncScope();
            var created = await scope.ServiceProvider.GetRequiredService<ISender>().Send(
                new CreateProductCommand(categoryId, "Queued product", "Description", 15m, 3));
            productId = created.Id;
            var context = scope.ServiceProvider.GetRequiredService<WriteDbContext>();
            var pending = Assert.Single(await context.Set<OutboxMessage>().ToListAsync());
            using var envelope = JsonDocument.Parse(pending.Body);
            queuedMessage = JsonSerializer.Deserialize<ProductCreatedV1>(
                envelope.RootElement.GetProperty("message").GetRawText(),
                new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
            Assert.Equal(productId, queuedMessage.ProductId);
            Assert.Equal(1, await context.Products.CountAsync());
        }

        await using var receiver = new ProductCreatedReceiver(fixture.RabbitMqConnectionString);
        await receiver.Start(); // Subscription exists before delivery is enabled.
        await using var restarted = await CatalogOutboxTestHost.Create(
            fixture, deliveryEnabled: true, connectionString);
        await restarted.Start();

        var delivered = await receiver.Receive(productId);
        Assert.Equal(queuedMessage, delivered);
        await restarted.WaitUntilOutboxDrained();
    }

    [Fact]
    public async Task WithoutRunningBus_CreateStillCommitsProductAndOutboxTogether()
    {
        await using var host = await CatalogOutboxTestHost.Create(fixture);
        var categoryId = await host.SeedCategory();
        // Do not start the host or broker connection: scoped publish must only enqueue.
        await using var scope = host.App.Services.CreateAsyncScope();

        var created = await scope.ServiceProvider.GetRequiredService<ISender>().Send(
            new CreateProductCommand(categoryId, "Offline", "Description", 20m, 4));

        await using var verification = host.App.Services.CreateAsyncScope();
        var context = verification.ServiceProvider.GetRequiredService<WriteDbContext>();
        Assert.True(await context.Products.AnyAsync(x => x.Id == created.Id));
        Assert.Equal(1, await context.Set<OutboxMessage>().CountAsync());
    }

    [Fact]
    public async Task OuterTransactionRollback_RemovesProductAndOutboxMessage()
    {
        await using var host = await CatalogOutboxTestHost.Create(fixture);
        var categoryId = await host.SeedCategory();
        await using (var scope = host.App.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<WriteDbContext>();
            await using var transaction = await context.Database.BeginTransactionAsync();
            await scope.ServiceProvider.GetRequiredService<ISender>().Send(
                new CreateProductCommand(categoryId, "Rolled back", "Description", 20m, 4));
            // The nested pipeline deliberately leaves saving to the outer transaction.
            await context.SaveChangesAsync();
            Assert.Equal(1, await context.Products.CountAsync());
            Assert.Equal(1, await context.Set<OutboxMessage>().CountAsync());

            await transaction.RollbackAsync();
        }

        await using var verification = host.App.Services.CreateAsyncScope();
        var persisted = verification.ServiceProvider.GetRequiredService<WriteDbContext>();
        Assert.Equal(0, await persisted.Products.CountAsync());
        Assert.Equal(0, await persisted.Set<OutboxMessage>().CountAsync());
        Assert.Equal(0, await persisted.Set<OutboxState>().CountAsync());
    }

    [Fact]
    public async Task DatabaseWriteFailure_DoesNotPersistProductOrEnqueuedEvent()
    {
        await using var host = await CatalogOutboxTestHost.Create(fixture);
        var categoryId = await host.SeedCategory();
        await using (var scope = host.App.Services.CreateAsyncScope())
        {
            // This passes application validation but cannot fit SQL decimal(18,2).
            await Assert.ThrowsAsync<DbUpdateException>(() =>
                scope.ServiceProvider.GetRequiredService<ISender>().Send(
                    new CreateProductCommand(categoryId, "Too expensive", "Description", decimal.MaxValue, 4)));
        }

        await using var verification = host.App.Services.CreateAsyncScope();
        var context = verification.ServiceProvider.GetRequiredService<WriteDbContext>();
        Assert.Equal(0, await context.Products.CountAsync());
        Assert.Equal(0, await context.Set<OutboxMessage>().CountAsync());
    }

    [Fact]
    public async Task RepeatedSave_AndProductUpdate_DoNotEnqueueAnotherCreatedEvent()
    {
        await using var host = await CatalogOutboxTestHost.Create(fixture);
        var categoryId = await host.SeedCategory();
        await using var scope = host.App.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<WriteDbContext>();
        var product = ProductAggregate.Create(categoryId, "First name", "Description", 5m, 2);
        context.Products.Add(product);

        // Exercise the bool overload as well: it must not bypass domain-event dispatch.
        await context.SaveChangesAsync(acceptAllChangesOnSuccess: true, CancellationToken.None);
        Assert.Empty(product.DomainEvents);
        await context.SaveChangesAsync();
        product.Update(null, "Renamed", null, null);
        await context.SaveChangesAsync();

        Assert.Equal(1, await context.Set<OutboxMessage>().CountAsync());
        Assert.Empty(product.DomainEvents);
    }

    [Fact]
    public async Task DuplicateProductRejection_DoesNotEnqueuePhantomCreatedEvent()
    {
        await using var host = await CatalogOutboxTestHost.Create(fixture);
        var categoryId = await host.SeedCategory();
        var command = new CreateProductCommand(categoryId, "Unique", "Description", 20m, 4);
        await using (var scope = host.App.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<ISender>().Send(command);

        await using (var scope = host.App.Services.CreateAsyncScope())
            await Assert.ThrowsAsync<Exception>(() =>
                scope.ServiceProvider.GetRequiredService<ISender>().Send(command));

        Assert.Equal(1, await host.PendingMessages());
    }

    [Fact]
    public async Task OutboxMigration_PreservesExistingProductStock_AndDoesNotReplayOldProducts()
    {
        await using var host = await CatalogOutboxTestHost.Create(fixture, migrate: false);
        await using var scope = host.App.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<WriteDbContext>();
        var migrator = context.GetService<IMigrator>();
        await migrator.MigrateAsync("20260514221719_addStock");
        var productId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO Categories (Id, Name, CreatedAt, CreatorId, ModifiedAt, ModifierId)
            VALUES ({categoryId}, N'Legacy category', {now}, {userId}, {now}, {userId});
            INSERT INTO Products (Id, CategoryId, Name, Description, Price, Stock,
                CreatedAt, CreatorId, ModifiedAt, ModifierId)
            VALUES ({productId}, {categoryId}, N'Legacy product', N'Existing data', 25.00, 9,
                {now}, {userId}, {now}, {userId});
            """);

        await migrator.MigrateAsync();

        var product = await context.Products.SingleAsync(x => x.Id == productId);
        Assert.Equal(9, product.Stock);
        Assert.Equal(25m, product.Price);
        Assert.Equal(0, await context.Set<OutboxMessage>().CountAsync());
        Assert.False(context.Database.HasPendingModelChanges());
    }
}
