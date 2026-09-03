using Domain.Abstractions;
using InventoryService.Domain.Aggregates;
using InventoryService.Domain.DomainEvents;

namespace InventoryService.UnitTest;

public sealed class InventoryItemCreationTests
{
    [Fact]
    public void Create_WithValidValues_InitializesInventoryAndRaisesEvent()
    {
        var productId = Guid.NewGuid();

        var inventory = InventoryItem.Create(productId, 12, 3);

        Assert.NotEqual(Guid.Empty, inventory.Id);
        Assert.Equal(productId, inventory.ProductId);
        Assert.Equal(12, inventory.OnHandQuantity);
        Assert.Equal(0, inventory.ReservedQuantity);
        Assert.Equal(12, inventory.AvailableQuantity);
        Assert.Equal(3, inventory.ReorderPoint);
        Assert.True(inventory.IsActive);
        Assert.Empty(inventory.Reservations);

        var domainEvent = Assert.IsType<InventoryItemCreatedDomainEvent>(
            Assert.Single(inventory.DomainEvents));

        Assert.Equal(inventory.Id, domainEvent.InventoryItemId);
        Assert.Equal(productId, domainEvent.ProductId);
        Assert.Equal(12, domainEvent.InitialQuantity);
        Assert.Equal(3, domainEvent.ReorderPoint);
        Assert.NotEqual(Guid.Empty, domainEvent.Id);
        Assert.Equal(DateTimeKind.Utc, domainEvent.OccurredOn.Kind);
    }

    [Fact]
    public void Create_WithEmptyProductId_ThrowsDomainException()
    {
        var exception = Assert.Throws<DomainException>(
            () => InventoryItem.Create(Guid.Empty));

        Assert.Contains("ProductId", exception.Message);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Create_WithNegativeInitialQuantity_ThrowsDomainException(
        int initialQuantity)
    {
        Assert.Throws<DomainException>(
            () => InventoryItem.Create(
                Guid.NewGuid(),
                initialQuantity));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Create_WithNegativeReorderPoint_ThrowsDomainException(
        int reorderPoint)
    {
        Assert.Throws<DomainException>(
            () => InventoryItem.Create(
                Guid.NewGuid(),
                initialQuantity: 10,
                reorderPoint));
    }
}
