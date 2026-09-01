using OrderService.Domain.Aggregates;
using OrderService.Domain.Enums;
using OrderService.Domain.ValueObjects;

namespace OrderService.UnitTests;

public class OrderDomainTests
{
    [Fact]
    public void Create_InitializesPendingOrder()
    {
        var customerId = Guid.NewGuid();
        var cartId = Guid.NewGuid();

        var order = OrderAggregate.Create(customerId, cartId);

        Assert.NotEqual(Guid.Empty, order.Id);
        Assert.Equal(customerId, order.CustomerId);
        Assert.Equal(cartId, order.CartId);
        Assert.Equal(OrderStatus.Pending, order.Status);
        Assert.Empty(order.Items);
        Assert.Empty(order.DomainEvents);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void Create_RejectsEmptyIds(bool validCustomer, bool validCart)
    {
        Assert.ThrowsAny<ArgumentException>(() => OrderAggregate.Create(
            validCustomer ? Guid.NewGuid() : Guid.Empty,
            validCart ? Guid.NewGuid() : Guid.Empty));
    }

    [Fact]
    public void AddItem_AddsItemAndCalculatesTotal()
    {
        var order = OrderAggregate.Create(Guid.NewGuid(), Guid.NewGuid());
        var productId = Guid.NewGuid();

        order.AddItem(productId, "Product", 12.50m, 4);

        var item = Assert.Single(order.Items);
        Assert.Equal(productId, item.ProductId);
        Assert.Equal("Product", item.ProductName);
        Assert.Equal(50m, order.TotalPrice);
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(1, 0)]
    public void AddItem_RejectsInvalidQuantityOrPrice(int quantity, decimal price)
    {
        var order = OrderAggregate.Create(Guid.NewGuid(), Guid.NewGuid());

        Assert.ThrowsAny<ArgumentException>(() =>
            order.AddItem(Guid.NewGuid(), "Product", price, quantity));
    }

    [Fact]
    public void OrderItem_UsesValueEquality()
    {
        var productId = Guid.NewGuid();
        var first = new OrderItem(productId, "Product", 10m, 2);
        var same = new OrderItem(productId, "Product", 10m, 2);
        var different = new OrderItem(productId, "Product", 10m, 3);

        Assert.Equal(first, same);
        Assert.NotEqual(first, different);
    }
}
