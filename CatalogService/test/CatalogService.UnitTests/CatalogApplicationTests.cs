using System.Linq.Expressions;
using CatalogService.Application.Features.Category.Commands.CreateCategory;
using CatalogService.Application.Features.Category.Commands.UpdateCategory;
using CatalogService.Application.Features.Product.Commands.CreateProduct;
using CatalogService.Application.Features.Product.Commands.UpdateProduct;
using CatalogService.Application.Features.Product.CreateProduct;
using CatalogService.Application.Features.Product.Queries.GetProduct;
using CatalogService.Application.Features.Product.Queries.GetProducts;
using CatalogService.Domain;
using CatalogService.Domain.Aggregates;
using CatalogService.Domain.Entities;
using Moq;

namespace CatalogService.UnitTests;

public class CatalogApplicationTests
{
    [Fact]
    public async Task CreateCategory_RejectsDuplicateName()
    {
        var read = new Mock<ICategoryReadRepository>();
        read.Setup(x => x.CategoryExists(
                It.IsAny<Expression<Func<CategoryEntity, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var write = new Mock<ICategoryWriteRepository>();

        var exception = await Assert.ThrowsAsync<Exception>(() =>
            new CreateCategoryCommandHandler(write.Object, read.Object)
                .Handle(new CreateCategoryCommand("Computers"), CancellationToken.None));

        Assert.Contains("already exists", exception.Message);
        write.Verify(x => x.AddCategory(It.IsAny<CategoryEntity>()), Times.Never);
    }

    [Fact]
    public async Task CreateCategory_AddsAndMapsCategory()
    {
        CategoryEntity? added = null;
        var read = new Mock<ICategoryReadRepository>();
        read.Setup(x => x.CategoryExists(
                It.IsAny<Expression<Func<CategoryEntity, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var write = new Mock<ICategoryWriteRepository>();
        write.Setup(x => x.AddCategory(It.IsAny<CategoryEntity>()))
            .Callback<CategoryEntity>(category => added = category)
            .Returns(Task.CompletedTask);

        var response = await new CreateCategoryCommandHandler(write.Object, read.Object)
            .Handle(new CreateCategoryCommand("Computers"), CancellationToken.None);

        Assert.NotNull(added);
        Assert.Equal(added.Id, response.Id);
        Assert.Equal("Computers", response.Name);
    }

    [Theory]
    [InlineData(false, 0, "Category")]
    [InlineData(true, 1, "already exists")]
    public async Task CreateProduct_RejectsInvalidRepositoryValidation(
        bool categoryExists, int duplicateCount, string expectedMessage)
    {
        var read = new Mock<IProductReadRepository>();
        read.Setup(x => x.ValidateProductExists(
                It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((categoryExists, duplicateCount));
        var write = new Mock<IProductWriteRepository>();

        var exception = await Assert.ThrowsAsync<Exception>(() =>
            new CreateProductCommandHandler(write.Object, read.Object)
                .Handle(NewProductCommand(), CancellationToken.None));

        Assert.Contains(expectedMessage, exception.Message);
        write.Verify(x => x.AddProduct(It.IsAny<ProductAggregate>()), Times.Never);
    }

    [Fact]
    public async Task CreateProduct_AddsAndMapsProduct()
    {
        ProductAggregate? added = null;
        var read = new Mock<IProductReadRepository>();
        read.Setup(x => x.ValidateProductExists(
                It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, 0));
        var write = new Mock<IProductWriteRepository>();
        write.Setup(x => x.AddProduct(It.IsAny<ProductAggregate>()))
            .Callback<ProductAggregate>(product => added = product);
        var command = NewProductCommand();

        var response = await new CreateProductCommandHandler(write.Object, read.Object)
            .Handle(command, CancellationToken.None);

        Assert.NotNull(added);
        Assert.Equal(added.Id, response.Id);
        Assert.Equal(command.Name, response.Name);
        Assert.Equal(command.Price, response.Price);
    }

    [Fact]
    public async Task UpdateProduct_UpdatesExistingProduct()
    {
        var product = ProductAggregate.Create(
            Guid.NewGuid(), "Old", "Description", 10m, 1);
        var read = new Mock<IProductReadRepository>();
        read.Setup(x => x.GetProductAsync(
                It.IsAny<Expression<Func<ProductAggregate, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);
        var write = new Mock<IProductWriteRepository>();

        var result = await new UpdateProductCommandHandler(write.Object, read.Object)
            .Handle(new UpdateProductCommand(product.Id, null, "New", 20m, null),
                CancellationToken.None);

        Assert.True(result);
        Assert.Equal("New", product.Name);
        Assert.Equal(20m, product.Price);
        write.Verify(x => x.UpdateProduct(product), Times.Once);
    }

    [Fact]
    public async Task UpdateProduct_RejectsMissingProduct()
    {
        var read = new Mock<IProductReadRepository>();
        read.Setup(x => x.GetProductAsync(
                It.IsAny<Expression<Func<ProductAggregate, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProductAggregate?)null);

        await Assert.ThrowsAsync<Exception>(() =>
            new UpdateProductCommandHandler(Mock.Of<IProductWriteRepository>(), read.Object)
                .Handle(new UpdateProductCommand(Guid.NewGuid(), null, null, null, null),
                    CancellationToken.None));
    }

    [Fact]
    public async Task GetProduct_ReturnsNullWhenRepositoryReturnsNull()
    {
        var read = new Mock<IProductReadRepository>();
        read.Setup(x => x.GetProductAsync(
                It.IsAny<Expression<Func<ProductAggregate, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProductAggregate?)null);

        var result = await new GetProductQueryHandler(read.Object)
            .Handle(new GetProductQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetProducts_MapsRepositoryResults()
    {
        var product = ProductAggregate.Create(
            Guid.NewGuid(), "Product", "Description", 10m, 2);
        var read = new Mock<IProductReadRepository>();
        read.Setup(x => x.GetProductsAsync()).ReturnsAsync([product]);

        var result = await new GetProductsQueryHandler(read.Object)
            .Handle(new GetProductsQuery(), CancellationToken.None);

        var dto = Assert.Single(result);
        Assert.Equal(product.Id, dto.Id);
        Assert.Equal(product.Name, dto.Name);
    }

    [Fact]
    public void Validators_RejectInvalidCommandsAndAcceptValidCommands()
    {
        Assert.False(new CreateCategoryCommandValidator()
            .Validate(new CreateCategoryCommand("")).IsValid);
        Assert.True(new CreateCategoryCommandValidator()
            .Validate(new CreateCategoryCommand("Valid")).IsValid);
        Assert.False(new UpdateCategoryCommandValidator()
            .Validate(new UpdateCategoryCommand(Guid.Empty, "")).IsValid);
        Assert.False(new CreateProductCommandValidator()
            .Validate(new CreateProductCommand(Guid.Empty, "", "", 0m, 0)).IsValid);
        Assert.False(new UpdateProductValidator()
            .Validate(new UpdateProductCommand(Guid.Empty, null, null, null, null)).IsValid);
    }

    private static CreateProductCommand NewProductCommand() => new(
        Guid.NewGuid(), "Laptop", "Description", 1200m, 4);
}
