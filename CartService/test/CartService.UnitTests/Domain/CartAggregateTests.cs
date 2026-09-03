using CartService.Domain.Aggregates;

namespace CartService.UnitTests;

public class CartAggregateTests
{
    [Fact]
    public void Create_InitializesCart()
    {
        var customerId = Guid.NewGuid();
        var before = DateTime.UtcNow;

        var cart = CartAggregate.Create(customerId);

        Assert.NotEqual(Guid.Empty, cart.Id);
        Assert.Equal(customerId, cart.CustomerId);
        Assert.True(cart.CreatedAt >= before);
        Assert.Empty(cart.Items);
        Assert.Equal(0m, cart.TotalPrice);
        Assert.False(cart.IsCheckedOut);
    }

    [Fact]
    public void Create_RejectsEmptyCustomerId()
    {
        Assert.ThrowsAny<ArgumentException>(() => CartAggregate.Create(Guid.Empty));
    }

    [Fact]
    public void AddItem_AddsNewItemAndCalculatesTotal()
    {
        var cart = CartAggregate.Create(Guid.NewGuid());
        var productId = Guid.NewGuid();

        cart.AddItem(productId, "Desk", 80m, 3);

        var item = Assert.Single(cart.Items);
        Assert.Equal(productId, item.ProductId);
        Assert.Equal("Desk", item.ProductName);
        Assert.Equal(3, item.Quantity);
        Assert.Equal(240m, cart.TotalPrice);
    }

    [Fact]
    public void AddItem_ForSameProduct_IncreasesQuantity()
    {
        var cart = CartAggregate.Create(Guid.NewGuid());
        var productId = Guid.NewGuid();

        cart.AddItem(productId, "Desk", 80m, 1);
        cart.AddItem(productId, "Desk", 80m, 2);

        Assert.Equal(3, Assert.Single(cart.Items).Quantity);
    }

    [Theory]
    [InlineData("", 10, 1)]
    [InlineData("Product", 0, 1)]
    [InlineData("Product", 10, 0)]
    public void AddItem_RejectsInvalidValues(string name, decimal price, int quantity)
    {
        var cart = CartAggregate.Create(Guid.NewGuid());

        Assert.ThrowsAny<ArgumentException>(() =>
            cart.AddItem(Guid.NewGuid(), name, price, quantity));
    }

    [Fact]
    public void RemoveItem_RemovesExistingItemAndIgnoresUnknownItem()
    {
        var cart = CartAggregate.Create(Guid.NewGuid());
        var productId = Guid.NewGuid();
        cart.AddItem(productId, "Desk", 80m, 1);

        cart.RemoveItem(Guid.NewGuid());
        Assert.Single(cart.Items);

        cart.RemoveItem(productId);
        Assert.Empty(cart.Items);
    }

    [Fact]
    public void ChangeItemQuantity_UpdatesExistingItem()
    {
        var cart = CartAggregate.Create(Guid.NewGuid());
        var productId = Guid.NewGuid();
        cart.AddItem(productId, "Desk", 80m, 1);

        cart.ChangeItemQuantity(productId, 4);

        Assert.Equal(4, Assert.Single(cart.Items).Quantity);
        Assert.Equal(320m, cart.TotalPrice);
    }

    [Fact]
    public void ChangeItemQuantity_RejectsMissingItem()
    {
        var cart = CartAggregate.Create(Guid.NewGuid());

        Assert.ThrowsAny<ArgumentException>(() =>
            cart.ChangeItemQuantity(Guid.NewGuid(), 1));
    }

    [Fact]
    public void Checkout_MarksCartAsCheckedOut()
    {
        var cart = CartAggregate.Create(Guid.NewGuid());
        cart.AddItem(Guid.NewGuid(), "Product", 1, 1);

        cart.Checkout();

        Assert.True(cart.IsCheckedOut);
    }
}
