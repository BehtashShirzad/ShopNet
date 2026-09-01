using Application.Abstractions.Contracts;
using CartService.Application;
using CartService.Application.Commands;
using CartService.Application.Query;
using CartService.Domain;
using CartService.Domain.Aggregates;
using Moq;
using ShopNet.Contracts.IntegrationEvents;

namespace CartService.UnitTests;

public class CartApplicationTests
{
    [Fact]
    public async Task AddCart_UsesCatalogDataAndStoresCart()
    {
        var userId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        CartAggregate? stored = null;
        var repository = new Mock<IRepository>();
        repository.Setup(x => x.StoreCart(It.IsAny<CartAggregate>()))
            .Callback<CartAggregate>(cart => stored = cart)
            .Returns(Task.CompletedTask);
        var catalog = new Mock<ICatalogService>();
        catalog.Setup(x => x.GetProduct(productId))
            .ReturnsAsync(new GetProductDto(productId, "Canonical name", 25m, 10));
        var command = new AddCartCommand([
            new ProductViewModelInput(productId, 2, 999m, "Untrusted name")
        ]) { UserId = userId };

        var id = await new AddCartCommandHandler(repository.Object, catalog.Object)
            .Handle(command, CancellationToken.None);

        Assert.NotNull(stored);
        Assert.Equal(id, stored.Id);
        Assert.Equal(userId, stored.CustomerId);
        var item = Assert.Single(stored.Items);
        Assert.Equal("Canonical name", item.ProductName);
        Assert.Equal(25m, item.Price);
        Assert.Equal(50m, stored.TotalPrice);
    }

    [Fact]
    public async Task AddCart_WhenCatalogProductIsMissing_DoesNotStoreCart()
    {
        var repository = new Mock<IRepository>();
        var catalog = new Mock<ICatalogService>();
        catalog.Setup(x => x.GetProduct(It.IsAny<Guid>()))
            .ReturnsAsync((GetProductDto?)null);
        var command = new AddCartCommand([
            new ProductViewModelInput(Guid.NewGuid(), 1, 10m, "Missing")
        ]) { UserId = Guid.NewGuid() };

        var exception = await Assert.ThrowsAsync<Exception>(() =>
            new AddCartCommandHandler(repository.Object, catalog.Object)
                .Handle(command, CancellationToken.None));

        Assert.Equal("Product Not Found", exception.Message);
        repository.Verify(x => x.StoreCart(It.IsAny<CartAggregate>()), Times.Never);
    }

    [Fact]
    public async Task AddProductToCart_RejectsUnknownOrForeignCart()
    {
        var repository = new Mock<IRepository>();
        repository.Setup(x => x.GetCart(It.IsAny<Guid>()))
            .ReturnsAsync((CartAggregate?)null);
        var handler = new AddProductToCartCommandHandler(
            repository.Object, Mock.Of<ICatalogService>());

        await Assert.ThrowsAsync<Exception>(() => handler.Handle(
            NewAddProductCommand(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None));

        var owner = Guid.NewGuid();
        repository.Setup(x => x.GetCart(It.IsAny<Guid>()))
            .ReturnsAsync(CartAggregate.Create(owner));

        await Assert.ThrowsAsync<Exception>(() => handler.Handle(
            NewAddProductCommand(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task AddProductToCart_UsesCatalogDataAndStoresUpdatedCart()
    {
        var owner = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var cart = CartAggregate.Create(owner);
        var repository = new Mock<IRepository>();
        repository.Setup(x => x.GetCart(cart.Id)).ReturnsAsync(cart);
        repository.Setup(x => x.StoreCart(cart)).Returns(Task.CompletedTask);
        var catalog = new Mock<ICatalogService>();
        catalog.Setup(x => x.GetProduct(productId))
            .ReturnsAsync(new GetProductDto(productId, "Server product", 14m, 8));
        var command = NewAddProductCommand(cart.Id, owner, productId);

        await new AddProductToCartCommandHandler(repository.Object, catalog.Object)
            .Handle(command, CancellationToken.None);

        var item = Assert.Single(cart.Items);
        Assert.Equal("Server product", item.ProductName);
        Assert.Equal(14m, item.Price);
        repository.Verify(x => x.StoreCart(cart), Times.Once);
    }

    [Fact]
    public async Task Checkout_RejectsInsufficientStockWithoutPublishing()
    {
        var owner = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var cart = CartAggregate.Create(owner);
        cart.AddItem(productId, "Product", 10m, 3);
        var repository = new Mock<IRepository>();
        repository.Setup(x => x.GetCart(cart.Id)).ReturnsAsync(cart);
        var catalog = new Mock<ICatalogService>();
        catalog.Setup(x => x.GetProduct(productId))
            .ReturnsAsync(new GetProductDto(productId, "Product", 10m, 2));
        var eventBus = new Mock<IIntegrationEventBus>();

        var exception = await Assert.ThrowsAsync<Exception>(() =>
            new CheckoutCartCommandHandler(repository.Object, catalog.Object, eventBus.Object)
                .Handle(new CheckoutCartCommand(cart.Id, owner), CancellationToken.None));

        Assert.Contains("out of stock", exception.Message);
        Assert.False(cart.IsCheckedOut);
        eventBus.Verify(x => x.PublishAsync(
            It.IsAny<CartCheckedOutEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Checkout_StoresCartAndPublishesCompleteEvent()
    {
        var owner = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var cart = CartAggregate.Create(owner);
        cart.AddItem(productId, "Product", 10m, 3);
        var repository = new Mock<IRepository>();
        repository.Setup(x => x.GetCart(cart.Id)).ReturnsAsync(cart);
        repository.Setup(x => x.StoreCart(cart)).Returns(Task.CompletedTask);
        var catalog = new Mock<ICatalogService>();
        catalog.Setup(x => x.GetProduct(productId))
            .ReturnsAsync(new GetProductDto(productId, "Product", 10m, 3));
        CartCheckedOutEvent? published = null;
        var eventBus = new Mock<IIntegrationEventBus>();
        eventBus.Setup(x => x.PublishAsync(
                It.IsAny<CartCheckedOutEvent>(), It.IsAny<CancellationToken>()))
            .Callback<CartCheckedOutEvent, CancellationToken>((message, _) => published = message)
            .Returns(Task.CompletedTask);

        var result = await new CheckoutCartCommandHandler(
                repository.Object, catalog.Object, eventBus.Object)
            .Handle(new CheckoutCartCommand(cart.Id, owner), CancellationToken.None);

        Assert.Equal(cart.Id, result);
        Assert.True(cart.IsCheckedOut);
        Assert.NotNull(published);
        Assert.Equal(cart.Id, published.CartId);
        Assert.Equal(30m, published.TotalPrice);
        Assert.Single(published.Items);
    }

    [Fact]
    public async Task UserCartQuery_ReturnsMappedCartForOwner()
    {
        var owner = Guid.NewGuid();
        var cart = CartAggregate.Create(owner);
        cart.AddItem(Guid.NewGuid(), "Product", 9m, 2);
        var repository = new Mock<IRepository>();
        repository.Setup(x => x.GetCart(cart.Id)).ReturnsAsync(cart);

        var result = await new UserCartQueryHandler(repository.Object)
            .Handle(new UserCartQuery(cart.Id, owner), CancellationToken.None);

        Assert.Equal(18m, result.TotalPrice);
        var product = Assert.Single(result.Products);
        Assert.Equal("Product", product.ProductName);
        Assert.Equal(2, product.Quantity);
    }

    [Fact]
    public async Task UserCartQuery_HidesForeignCart()
    {
        var cart = CartAggregate.Create(Guid.NewGuid());
        var repository = new Mock<IRepository>();
        repository.Setup(x => x.GetCart(cart.Id)).ReturnsAsync(cart);

        await Assert.ThrowsAsync<Exception>(() =>
            new UserCartQueryHandler(repository.Object)
                .Handle(new UserCartQuery(cart.Id, Guid.NewGuid()), CancellationToken.None));
    }

    private static AddProductToCartCommand NewAddProductCommand(
        Guid cartId, Guid userId, Guid? productId = null) => new()
    {
        CartId = cartId,
        UserId = userId,
        ProductDto = new ProductViewModelInput(
            productId ?? Guid.NewGuid(), 2, 999m, "Client value")
    };
}
