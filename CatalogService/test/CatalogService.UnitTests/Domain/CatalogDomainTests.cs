using CatalogService.Domain;
using CatalogService.Domain.Aggregates;
using CatalogService.Domain.DomainEvents;
using CatalogService.Domain.Entities;

namespace CatalogService.UnitTests;

public class CatalogDomainTests
{
    [Fact]
    public void ProductCreate_InitializesProductAndRaisesCreatedEvent()
    {
        var categoryId = Guid.NewGuid();

        var product = ProductAggregate.Create(
            categoryId, "Laptop", "Description", 1200m);

        Assert.NotEqual(Guid.Empty, product.Id);
        Assert.Equal(categoryId, product.CategoryId);
        Assert.Equal("Laptop", product.Name);
        Assert.Equal("Description", product.Description);
        Assert.Equal(1200m, product.Price);
        var domainEvent = Assert.IsType<ProductCreatedDomainEvent>(
            Assert.Single(product.DomainEvents));
        Assert.Equal(product.Id, domainEvent.ProductId);
        Assert.NotEqual(Guid.Empty, domainEvent.Id);
        Assert.NotEqual(product.Id, domainEvent.Id);
    }

    [Theory]
    [InlineData(false, "Product", 10)]
    [InlineData(true, "", 10)]
    [InlineData(true, "Product", 0)]
    public void ProductCreate_RejectsInvalidRequiredValues(
        bool validCategory, string name, decimal price)
    {
        var categoryId = validCategory ? Guid.NewGuid() : Guid.Empty;

        Assert.ThrowsAny<ArgumentException>(() => ProductAggregate.Create(
            categoryId, name, "Description", price));
    }

    [Fact]
    public void ProductUpdate_ChangesFieldsAndRaisesExpectedEvents()
    {
        var product = ProductAggregate.Create(
            Guid.NewGuid(), "Old", "Old description", 10m);
        product.ClearEvents();
        var newCategoryId = Guid.NewGuid();

        product.Update(newCategoryId, "New", 15m, "New description");

        Assert.Equal(newCategoryId, product.CategoryId);
        Assert.Equal("New", product.Name);
        Assert.Equal(15m, product.Price);
        Assert.Equal("New description", product.Description);
        Assert.Contains(product.DomainEvents, x => x is ProductPriceChangedDomainEvent);
        Assert.Contains(product.DomainEvents, x => x is ProductUpdatedDomainEvent);
    }

    [Fact]
    public void ProductUpdate_WithNoChanges_DoesNotRaiseEvent()
    {
        var categoryId = Guid.NewGuid();
        var product = ProductAggregate.Create(
            categoryId, "Product", "Description", 10m);
        product.ClearEvents();

        product.Update(categoryId, "Product", 10m, "Description");

        Assert.Empty(product.DomainEvents);
    }

    [Fact]
    public void ProductUpdate_RejectsInvalidPrice()
    {
        var product = ProductAggregate.Create(
            Guid.NewGuid(), "Product", "Description", 10m);

        Assert.ThrowsAny<ArgumentException>(() =>
            product.Update(null, null, 0m, null));
    }

    [Fact]
    public void Category_CreateAndUpdate_EnforceNameInvariant()
    {
        var category = CategoryEntity.Create("Computers");

        category.Update("Accessories");

        Assert.NotEqual(Guid.Empty, category.Id);
        Assert.Equal("Accessories", category.Name);
        Assert.ThrowsAny<ArgumentException>(() => CategoryEntity.Create(""));
        Assert.ThrowsAny<ArgumentException>(() => category.Update(""));
    }
}
