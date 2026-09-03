using Application.Abstractions.Contracts;
using CatalogService.Domain.Aggregates;
using CatalogService.Domain.Entities;
using CatalogService.Infrastructure;
using Domain.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.IntegrationTests;

[Collection(CatalogContainersCollection.Name)]
public class CatalogPersistenceIntegrationTests(CatalogContainersFixture fixture)
{
    [Fact]
    public async Task Repositories_PersistQueryAuditAndDispatchDomainEvents()
    {
        var userId = Guid.NewGuid();
        var domainEventBus = new RecordingDomainEventBus();
        var writeOptions = new DbContextOptionsBuilder<WriteDbContext>()
            .UseSqlServer(fixture.DatabaseConnectionString)
            .Options;
        await using var writeContext = new WriteDbContext(
            writeOptions, domainEventBus, new TestCurrentUser(userId));
        await writeContext.Database.MigrateAsync();
        var priceProperty = writeContext.Model.FindEntityType(typeof(ProductAggregate))!
            .FindProperty(nameof(ProductAggregate.Price))!;
        Assert.Equal(18, priceProperty.GetPrecision());
        Assert.Equal(2, priceProperty.GetScale());
        var category = CategoryEntity.Create("Computers");
        await new CategoryWriteRepository(writeContext).AddCategory(category);
        await writeContext.SaveChangesAsync();
        var product = ProductAggregate.Create(
            category.Id, "Laptop", "Description", 1200m);
        new ProductWriteRepository(writeContext).AddProduct(product);

        await writeContext.SaveChangesAsync();

        Assert.Equal(userId, category.CreatorId);
        Assert.Equal(userId, product.CreatorId);
        Assert.NotEqual(default, product.CreatedAt);
        Assert.Single(domainEventBus.Events);
        Assert.Empty(product.DomainEvents);

        var queryOptions = new DbContextOptionsBuilder<QueryDbContext>()
            .UseSqlServer(fixture.DatabaseConnectionString)
            .Options;
        await using var queryContext = new QueryDbContext(queryOptions);
        var productRead = new ProductReadRepository(queryContext);
        var validation = await productRead.ValidateProductExists(
            "Laptop", category.Id, CancellationToken.None);
        var loaded = await productRead.GetProductAsync(x => x.Id == product.Id);

        Assert.True(validation.CategoryExsits);
        Assert.Equal(1, validation.ProductNameCount);
        Assert.NotNull(loaded);
        Assert.Equal("Laptop", loaded.Name);
        Assert.True(await new CategoryReadRepository(queryContext)
            .CategoryExists(x => x.Name == "Computers", CancellationToken.None));
    }

    private sealed class TestCurrentUser(Guid userId) : ICurrentUser
    {
        public string UserId { get; } = userId.ToString();
    }

    private sealed class RecordingDomainEventBus : IDomainEventBus
    {
        public List<IDomainEvent> Events { get; } = [];

        public Task PublishAsync<T>(T domainEvent, CancellationToken cancellationToken = default)
            where T : IDomainEvent
        {
            Events.Add(domainEvent);
            return Task.CompletedTask;
        }
    }
}
